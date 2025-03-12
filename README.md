# vibe-gamedev
**Letting AI agents use the Unity editor.**

`vibe-gamedev` is a Unity package that creates an interface between the Unity editor and AI agents, allowing end-to-end [vibe coding](https://en.wikipedia.org/wiki/Vibe_coding) for game development. It serializes GameObjects to JSON files that can be read and edited by agents, then deserializes those files back into updated GameObjects.

TODO: video

*This is highly experimental!* It will probably break, and data loss is a possibility.

## Getting started
### Installing `vibe-gamedev`
With your project open in the Unity editor, install the package:
```
Window > Package Manager > + > Install package from git URL...
```
`vibe-gamedev` will start when the editor rebuilds.

### Using an AI agent
Whenever you open or save a scene in the Unity editor, `vibe-gamedev` serializes each object in the scene as a JSON file. By default, these JSON files are saved to a folder in your project called `VibeGamedev/`.

Your AI agent (Claude Desktop, Cursor, etc.) is then free to manipulate these JSON files — changing component properties, adding or removing components, adding or removing GameObjects. Each change will automatically trigger a corresponding change in the editor.

You will likely want to provide your AI agent with a rule describing how to use `vibe-gamedev`; you can find an example rule in `Samples~/agent-rule.mdc`.

## Changing the behavior of `vibe-gamedev`
### Pausing, logs, and changing the serialization directory
You can change the settings and view the logs of `vibe-gamedev` by opening the `Tools > Vibe Gamedev` window from the Unity menu bar.

### Serializing a new data type
Each data type that is serialized has a corresponding implementation of the `IValueParser` interface. While some are defined by default, you can define your own by implementing this interface, as in `Samples~/CustomValueParser.cs`. Make sure to call `IValueParser.RegisterParser()` somewhere in your Editor code.

### Changing what gets serialized for a particular `Component`
By default, `vibe-gamedev` will try to serialize all of the component properties that appear in the editor's Inspector pane. However, (a) my logic for doing this is certainly not correct, and (b) you may not want to serialize everything, especially for complex components. If you want to serialize a specific set of properties, you can implement the `IComponentPropertiesOverride` interface, as in `Samples~/CustomerPropertyOverride.cs`. Make sure to call `IComponentPropertiesOverride.RegisterOverride()` somewhere in your Editor code.

## Why JSON files instead of MCP?
The [Model Context Protocol](https://modelcontextprotocol.io/introduction) (MCP) is a standard for creating tools that AI agents can interact with. An MCP tool consists of a server that the agent can use to gather information and make changes to external systems. `vibe-gamedev` does not use MCP.

I did experiment with building an MCP server similar to [Arodroid's](https://github.com/Arodoid/UnityMCP/tree/main/UnityMCPPlugin), but opted to use JSON serialization for a few reasons:

1. I did not like having to run an MCP server in parallel to the Unity editor. Ideally, `vibe-gamedev` would run with no extra processes or effort.
2. The agent I experimented with (Claude 3.7 Sonnet) was quite competent working with file systems, already having tools like `grep` to quickly understand the scene. Using a new server that the model has no prior training with seemed riskier.
3. Designing the server API felt challenging. For example, it wasn't clear how to let the agent learn about the scene: dumping the entire serialization at once would flood the model context window with excess info, while allowing more specificity seemed like rolling my own query language. I decided I'd keep things familiar and open-ended by using the file system.

## Current limitations
- [ ] Does not currently support Prefabs. `.prefab` files behave quite differently from GameObjects in an active scene. Of course, if you're vibe coding, who cares about reusability?
- [ ] Only the active scene is serialized/deserialized, so your agent can only "see" and manipulate the active scene.
- [ ] I am not a Unity master. There are probably dumb assumptions made in here about how GameObjects or Components work. Please open a PR when you find them!
