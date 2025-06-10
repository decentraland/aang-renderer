# Aange Renderer

This project is responsible for rendering previews of the user profile and wearables used on the Decentraland Marketplace, Authentication screen, Profile page, and the builder.

It builds as a Web target, uses WebGPU as it's rendering backend, and shares the Toon and Scene shaders with the Explorer client so we get the same visual representation as the users will see in the game

## Usage

Primarily the renderer will fetch it's configuration from URL parameters passed to it, but can also be configured dynamically after initial load via a SendMessage call.

The renderer can run in three different modes, depending on it's usage: Marketplace, Authentication, Profile, Builder

## Parameters

* `mode`: Which mode we run as. Possible values:
  * `marketplace` (default)
  * `authentication`
  * `profile`
  * `builder`
* `profile`: The id (wallet address) of the profile to use or one of the default profiles (`default1` - `default15`)
* `emote`: The default emote that the avatar will play. Only used if no emote override is present. Possible values:
  * `idle` (default)
  * `clap`
  * `dab`
  * `dance`
  * `fashion`
  * `fashion-2`
  * `fashion-3`
  * `fashion-4`
  * `love`
  * `money`
  * `fist-pump`
  * `head-explode`
* `urn`: An URN address of a wearable or emote to load. It will override any existing wearable in the same category already present on the profile that has been loaded. Can be included multiple times in `builder` mode to load multiple wearables.
* `background`: The background color to use for the renderer. It must be in hex and not include the leading # (e.g. `ff00ff`). It may include alpha for a transparent background. Default is transparent.
* `skinColor`: The color to use for the skin of the character. It must be in hex and not include the leading # (e.g. `ff00ff`).
* `hairColor`: The color to use for the hair of the character. It must be in hex and not include the leading # (e.g. `ff00ff`).
* `eyeColor`: The color to use for the eyes of the character. It must be in hex and not include the leading # (e.g. `ff00ff`).
* `bodyShape`: The body shape to use. Possible values:
  * `urn:decentraland:off-chain:base-avatars:BaseMale`
  * `urn:decentraland:off-chain:base-avatars:BaseFemale`
* `projection`: The projection to use for the camera. Possible values:
  * `perspective` (default)
  * `orthographic`
* `base64`: A base64 encoded definition of a wearable or emote to load.
* `contract`: The contract address of a wearable or emote to load.
* `item`: The item id of a wearable or emote to load.
* `token`: The token id of a wearable or emote to load.
* `env`: The environment to use for API calls. Possible values:
  * `prod` (default) - uses ORG
  * `dev` - uses ZONE


## Modes

Depending on the mode, not all parameters are used. These are the valid parameters in each mode:

### Marketplate
* `background`
* `profile`
* `urn` or `contract` & `item` or `contract` & `token`
* `emote`

### Profile
* `background`
* `profile`
* `emote`

### Authentication
* `background`
* `profile`
* `emote`

### Builder
* `background`
* `bodyShape`
* `eyeColor`
* `hairColor`
* `skinColor`
* `urn`
  * Multiple urn parameters may be used to load several wearables. The categories of the wearables must be unique (e.g. two urns cannot both be for "upper_body")
* `emote`
* `base64`

## Dynamic configuration

Most properties can be set dynamically after the renderer is already running by calling `SendMessage('JSBridge', 'MethodName', 'value')` on the `unityInstance` object returned after initialization.

Example usage:

```javascript
unityInstance.SendMessage('JSBridge', 'SetEmote', 'clap');
unityInstance.SendMessage('JSBridge', 'SetSkinColor', 'ff0000');
```

Every call of this function will trigger a reload of the entire avatar.
For a full list of available functions check [JSBridge](Assets/Scripts/JSBridge.cs).

### Special cases

* `SetUrns`
  * The input should be either a single URN or a list of urns separated by commas.
  * Example: `unityInstance.SendMessage('JSBridge', 'SetUrns', 'urn:decentraland:off-chain:base-avatars:kilt,urn:decentraland:off-chain:base-avatars:full_beard,urn:decentraland:off-chain:base-avatars:blue_bandana');`