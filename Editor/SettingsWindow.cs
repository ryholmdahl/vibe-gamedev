using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace VibeGamedev
{
    public class SettingsWindow : EditorWindow
    {
        private string selectedDirectory = "";
        private Vector2 logScrollPosition;
        private static readonly List<string> logs = new();
        private static SettingsWindow instance;

        [MenuItem("Tools/Vibe Gamedev")]
        public static void ShowWindow()
        {
            instance = GetWindow<SettingsWindow>("Vibe Gamedev");
        }

        private void OnEnable()
        {
            selectedDirectory = ChangeExecutor.GetSerializationPath();
        }

        private void SetSelectedDirectory(string directory)
        {
            ChangeExecutor.SetSerializationPath(directory);
            selectedDirectory = ChangeExecutor.GetSerializationPath();
        }

        public static void Log(string message)
        {
            if (logs.Count > 1000) // Keep the last 1000 logs
            {
                logs.RemoveAt(0);
            }
            logs.Add(message);

            if (instance != null)
            {
                instance.Repaint();
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Directory Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            string newDirectory = EditorGUILayout.DelayedTextField("Directory Path", selectedDirectory);
            if (newDirectory != selectedDirectory)
            {
                SetSelectedDirectory(newDirectory);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Directory", selectedDirectory, "");
                if (!string.IsNullOrEmpty(path))
                {
                    SetSelectedDirectory(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(ChangeExecutor.IsPaused() ? "Start" : "Stop"))
            {
                if (ChangeExecutor.IsPaused())
                {
                    ChangeExecutor.Resume();
                }
                else
                {
                    ChangeExecutor.Pause();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Log pane
            GUILayout.Label("Logs", EditorStyles.boldLabel);
            logScrollPosition = EditorGUILayout.BeginScrollView(logScrollPosition, GUILayout.ExpandHeight(true));
            foreach (string log in logs)
            {
                EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}