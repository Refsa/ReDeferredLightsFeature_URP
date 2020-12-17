# Deferred Lights Feature for Unity URP
**Disclaimer: This project is pretty much abandoned, but still might contain useful information for others**

![image](https://user-images.githubusercontent.com/4514574/102427138-06f0d400-4011-11eb-85f5-8b6472830169.png)

This package contains the elements to have tiled deferred lights as a render feature for URPs Forward rendering mode. There is also support for Forward+ rendering with access to the light data in Shader Graph.

Issues:
- GBuffer passes are slow on complex scene
- Limited alpha support in deferred pass mode
- Tiling is sub-optimal when there is a large depth discrepancy. This can be fixed with better tiling or changing it to clustering.
- Scene View support is buggy at best
- Only point lights are currently implemented
- No shadows
- Currently requires a custom material for the deferred pass to work, this is not the case for forward+ mode

Positives:
- Support for hundreds to thousands of point lights depending on scene complexity
- Can retreive light data in shader graph and regular forward mode shaders, meaning you can have more active lights in the scene at any given time
- Uses same lighting calculation as the regular URP shaders, so it follows the same style