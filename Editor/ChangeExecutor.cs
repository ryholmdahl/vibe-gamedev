using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

namespace VibeGamedev
{
    /// <summary>
    /// The primary class that manages serialization of changes made in the editor, and deserialization of changes made to JSON files.
    /// </summary>
    [InitializeOnLoad]
    public class ChangeExecutor
    {
        private const string DIRECTORY_PREF_KEY = "VibeGamedev_SelectedDirectory";
        private const string IS_PAUSED_PREF_KEY = "VibeGamedev_IsPaused";
        private const string DEFAULT_DIRECTORY = "VibeGamedev";

        private static readonly Dictionary<GameObject, string> gameObjectToId = new();
        private static readonly Dictionary<string, GameObject> idToGameObject = new();

        private static FileEditWatcher jsonWatcher;

        private static bool isPaused;
        private static bool hasCompletedInitialSerialization = false;
        private static string serializationPath;

        static ChangeExecutor()
        {
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.update += OnUpdate;
            EditorApplication.quitting += OnEditorQuitting;
            isPaused = EditorPrefs.GetBool(IS_PAUSED_PREF_KEY, false);
            SetSerializationPath(EditorPrefs.GetString(DIRECTORY_PREF_KEY, DEFAULT_DIRECTORY));
        }

        /// <summary>
        /// Pauses the change executor, preventing it from serializing or deserializing.
        /// When unpaused, it will trigger a full re-serialization to make sure everything is up-to-date.
        /// </summary>
        public static void Pause()
        {
            isPaused = true;
            jsonWatcher?.Dispose();
            jsonWatcher = null;
            // Force re-serialization when unpaused
            hasCompletedInitialSerialization = false;
            EditorPrefs.SetBool(IS_PAUSED_PREF_KEY, isPaused);
            SettingsWindow.Log("Paused");
        }

        /// <summary>
        /// Unpauses the change executor, allowing it to serialize and deserialize.
        /// </summary>
        public static void Resume()
        {
            isPaused = false;
            EditorPrefs.SetBool(IS_PAUSED_PREF_KEY, isPaused);
            SettingsWindow.Log("Resumed");
        }

        public static bool IsPaused() => isPaused;

        /// <summary>
        /// Gets the path to the directory where serialized files are stored.
        /// </summary>
        public static string GetSerializationPath()
        {
            if (serializationPath == null)
            {
                SetSerializationPath(EditorPrefs.GetString(DIRECTORY_PREF_KEY, DEFAULT_DIRECTORY));
            }
            return serializationPath;
        }

        /// <summary>
        /// Sets the path to the directory where serialized files are stored and triggers a full re-serialization.
        /// </summary>
        public static void SetSerializationPath(string newPath)
        {
            if (newPath != serializationPath)
            {
                if (!Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                }
                jsonWatcher?.Dispose();
                jsonWatcher = null;
                if (serializationPath != null)
                {
                    Directory.Delete(serializationPath, true);
                }
                EditorPrefs.SetString(DIRECTORY_PREF_KEY, newPath);
                serializationPath = newPath;
                hasCompletedInitialSerialization = false;
                SettingsWindow.Log("Set serialization path to " + newPath);
            }
        }

        static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            if (isPaused)
            {
                return;
            }
            SerializeScene(scene);
        }

        static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            if (isPaused)
            {
                return;
            }
            // When a new scene is opened, nuke the object-id mappings and start fresh
            gameObjectToId.Clear();
            idToGameObject.Clear();
            SerializeScene(scene);
        }

        /// <summary>
        /// Serializes the objects in a scene to the serialization path.
        /// </summary>
        /// <param name="scene">The scene to serialize.</param>
        private static void SerializeScene(UnityEngine.SceneManagement.Scene scene)
        {
            jsonWatcher?.Pause();
            string sceneName = scene.name;
            if (sceneName == "")
            {
                return;
            }
            // Create a subfolder for this scene
            string sceneOutputDir = serializationPath;
            if (!hasCompletedInitialSerialization)
            {
                if (Directory.Exists(sceneOutputDir))
                {
                    Directory.Delete(sceneOutputDir, true);
                }
            }
            if (!Directory.Exists(sceneOutputDir))
            {
                Directory.CreateDirectory(sceneOutputDir);
            }

            // Get all root GameObjects in the scene
            GameObject[] rootObjects = scene.GetRootGameObjects().ToArray();
            // Process each GameObject and its children
            foreach (GameObject rootObject in rootObjects)
            {
                SerializeObject(rootObject, sceneOutputDir);
            }
            DeleteUnusedSerializations(rootObjects, sceneOutputDir);
            SettingsWindow.Log($"Scene serialized: {scene.name}");
            // We don't unpause the watcher here, since it may have already been paused for another reason
            // (e.g., perhaps the initial serialization is underway). It will be unpaused when the next editor frame occurs.
        }

        /// <summary>
        /// Deletes unused serializations in the output directory.
        /// A serialization is "unused" if it does not map to an object in `existingObjects`, or if it maps
        /// to an object that has another corresponding serialization in the same directory.
        /// </summary>
        /// <param name="existingObjects">The existing objects in the scene.</param>
        /// <param name="parentDir">The directory of serializations that should be checked.</param>
        private static void DeleteUnusedSerializations(GameObject[] existingObjects, string parentDir)
        {
            HashSet<string> existingObjectIDs = new(existingObjects.Select(ObjectToID));

            foreach (string path in Directory.GetDirectories(parentDir))
            {
                bool isUsed = false;
                if (File.Exists(Path.Combine(path, "info.json")))
                {
                    string jsonContent = File.ReadAllText(Path.Combine(path, "info.json"));
                    var jsonObject = JsonUtility.FromJson<Serialization.SerializedObject>(jsonContent);
                    if (existingObjectIDs.Contains(jsonObject.id))
                    {
                        isUsed = true;
                        existingObjectIDs.Remove(jsonObject.id);
                    }
                    else if (idToGameObject.ContainsKey(jsonObject.id) && idToGameObject[jsonObject.id] == null)
                    {
                        // If the object was destroyed, allow its ID to be reused. If it was just moved, don't
                        idToGameObject.Remove(jsonObject.id);
                    }
                }
                if (!isUsed)
                {
                    SettingsWindow.Log($"Deleting folder for unknown or duplicate object: {path}");
                    Directory.Delete(path, true);
                }
            }
        }

        /// <summary>
        /// Creates a new folder for an object with an `info.json` file, and then recurses for each child object.
        /// Child object subdirectories will be created within the parent folder.
        /// Any folders in the new directory that do not map to a child will be removed.
        /// </summary>
        /// <param name="gameObject">The object to serialize.</param>
        /// <param name="parentDir">The directory where the object's corresponding subdirectory should be created.</param>
        private static void SerializeObject(GameObject gameObject, string parentDir)
        {
            string objectDirectory = Path.Combine(parentDir, IDToDesiredDirectoryName(ObjectToID(gameObject)));
            if (!Directory.Exists(objectDirectory))
            {
                Directory.CreateDirectory(objectDirectory);
            }
            string jsonContent = new Serialization.SerializedObject(gameObject).ToString();
            string jsonPath = Path.Combine(objectDirectory, $"info.json");
            if (!File.Exists(jsonPath) || File.ReadAllText(jsonPath) != jsonContent)
            {
                File.WriteAllText(jsonPath, jsonContent);
            }
            GameObject[] children = gameObject.transform.Cast<Transform>().Select(child => child.gameObject).ToArray();
            foreach (GameObject child in children)
            {
                SerializeObject(child, objectDirectory);
            }
            DeleteUnusedSerializations(children, objectDirectory);
        }

        static void OnUpdate()
        {
            if (isPaused)
            {
                return;
            }
            if (!hasCompletedInitialSerialization)
            {
                OnSceneOpened(EditorSceneManager.GetActiveScene(), OpenSceneMode.Single);
                hasCompletedInitialSerialization = true;
            }
            jsonWatcher ??= new FileEditWatcher(serializationPath);
            // Resume the watcher after any prior work has been completed, so we don't listen to changes made during serialization
            jsonWatcher.Resume();
            if (!jsonWatcher.IsQueueEmpty() && EditorWindow.focusedWindow != null)
            {
                jsonWatcher.Pause();
                var jsonChanges = jsonWatcher.ReadQueue();
                UpdateSceneWithJSONChanges(jsonChanges);
                if (IValueParser.UnsupportedTypesFound.Count > 0)
                {
                    File.WriteAllText(Path.Combine(serializationPath, "unsupported_types.txt"), string.Join("\n", IValueParser.UnsupportedTypesFound));
                }
            }
        }

        /// <summary>
        /// Applies changes to the scene based on the changes described in `jsonChanges`.
        /// Currently, this might create a GameObject, edit a GameObject, or delete a GameObject.
        /// </summary>
        /// <param name="jsonChanges">The changes to apply.</param>
        private static void UpdateSceneWithJSONChanges(List<FileEditWatcher.FileAction> jsonChanges)
        {
            // Accumulate which properties will be set and which objects need to be serialized as we
            // execute the changes. We don't want to do this during execution because it might cause
            // properties or parentage to refer to not-yet-existing objects/components.
            List<Action> propertySetActions = new();
            List<Action> serializeActions = new();
            foreach (FileEditWatcher.FileAction action in jsonChanges)
            {
                if (action.actionType == FileEditWatcher.FileActionType.Edit)
                {
                    SettingsWindow.Log("Edit action on scene object, creating or editing: " + action.jsonPath);
                    var jsonObject = JsonUtility.FromJson<Serialization.SerializedObject>(action.jsonContent);
                    GameObject gameObject = jsonObject.ToGameObject();
                    foreach (var serializedComponent in jsonObject.components)
                    {
                        try
                        {
                            propertySetActions.Add(serializedComponent.TryAddTo(gameObject));
                        }
                        catch (ArgumentException)
                        {
                            SettingsWindow.Log("No type found for component " + serializedComponent.componentName + ", skipping");
                        }
                    }
                    string parentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(action.jsonPath));
                    serializeActions.Add(() =>
                    {
                        string parentJsonPath = Path.Combine(parentDirectory, "info.json");
                        if (File.Exists(parentJsonPath))
                        {
                            string parentJsonContent = File.ReadAllText(parentJsonPath);
                            var parentJsonObject = JsonUtility.FromJson<Serialization.SerializedObject>(parentJsonContent);
                            string parentId = parentJsonObject.id;
                            GameObject parent = IDToObject(parentId);
                            gameObject.transform.SetParent(parent.transform);
                        }
                        else
                        {
                            gameObject.transform.SetParent(null);
                        }
                        // For some reason, tag gets unset; reset it here
                        gameObject.tag = jsonObject.tag;
                        SerializeObject(gameObject, parentDirectory);
                    });
                    SettingsWindow.Log($"Edited {jsonObject.id}");
                }
                else if (action.actionType == FileEditWatcher.FileActionType.Delete)
                {
                    GameObject gameObject = null;
                    try
                    {
                        gameObject = IDToObject(JSONPathToID(action.jsonPath));
                    }
                    catch (ArgumentException) { }
                    if (gameObject == null)
                    {
                        SettingsWindow.Log("Delete action on non-existent object, skipping: " + action.jsonPath);
                    }
                    else
                    {
                        EditorSceneManager.MarkSceneDirty(gameObject.scene);
                        Undo.DestroyObjectImmediate(gameObject);
                        SettingsWindow.Log($"Destroyed {gameObjectToId[gameObject]}");
                    }
                }
                else
                {
                    throw new NotImplementedException(action.actionType.ToString());
                }
            }
            foreach (Action propertySetAction in propertySetActions)
            {
                propertySetAction();
            }
            foreach (Action serializeAction in serializeActions)
            {
                serializeAction();
            }
        }

        private static void OnEditorQuitting()
        {
            jsonWatcher?.Dispose();
            jsonWatcher = null;
        }

        public static string ObjectToID(GameObject gameObject)
        {
            if (gameObjectToId.ContainsKey(gameObject))
            {
                return gameObjectToId[gameObject];
            }
            // Replace invalid file name characters with underscores
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string filename = gameObject.name;
            Transform parent = gameObject.transform.parent;
            while (parent != null)
            {
                filename = parent.gameObject.name + "->" + filename;
                parent = parent.parent;
            }
            foreach (char c in invalidChars)
            {
                filename = filename.Replace(c, '_');
            }
            if (idToGameObject.ContainsKey(filename))
            {
                int cnt = 0;
                while (idToGameObject.ContainsKey(filename + " (" + cnt + ")"))
                {
                    cnt++;
                }
                filename += " (" + cnt + ")";
            }
            gameObjectToId[gameObject] = filename;
            idToGameObject[filename] = gameObject;
            return filename;
        }

        public static GameObject IDToObject(string id)
        {
            if (idToGameObject.ContainsKey(id))
            {
                return idToGameObject[id];
            }
            throw new ArgumentException($"Could not find object for id: {id}");
        }

        public static void SetID(GameObject gameObject, string id)
        {
            gameObjectToId[gameObject] = id;
            idToGameObject[id] = gameObject;
        }

        private static string IDToDesiredDirectoryName(string id)
        {
            return id.Split("->").Last();
        }

        private static string JSONPathToID(string jsonPath)
        {
            // This is a hack, since the file may have been moved and so its ID may not align with its parent's.
            // But we're keeping it because otherwise it's not clear how to get the ID of a deleted object
            return Path.GetFileName(Path.GetDirectoryName(jsonPath));
        }
    }
}