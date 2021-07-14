# Deferred Lights Feature for Unity URP
**Disclaimer: This project is pretty much abandoned, but still might contain useful information for others. Parts of it is under developed and other parts might be ready for use**

![image](https://user-images.githubusercontent.com/4514574/102427138-06f0d400-4011-11eb-85f5-8b6472830169.png)

This package contains the elements to have tiled deferred lights as a render feature for URPs Forward rendering mode. There is also support for Forward+ rendering with access to the light data in Shader Graph. Currently it is no where near production ready, but might be fun to explore for other use cases.

I am working on a refactoring/remake of this for production ready use, but thought it might be useful for other to see more in-depth use cases for the SRP features within the URP workflow.

Issues:
- GBuffer passes are slow on complex scene
- Limited alpha support in deferred pass mode
- Tiling is sub-optimal when there is a large depth discrepancy. This can be fixed with better tiling or changing it to clustering.
- Scene View support is buggy at best and is disabled atm
- Only point lights are currently implemented
- No shadows
- Currently requires a custom material for the deferred pass to work, this is not the case for forward+ mode
- plethora of other small issues that needs to be worked out
- Obviously there is less room for variety in materials using deferred pass
- Only a very simplistic tiling technique is included, doesnt handle large depth discrepancies inside tiles very well.
- And lastly, there is a lot of dead and broken code

Positives:
- Support for hundreds to thousands of point lights depending on scene complexity
- works side by side with the forward renderer of URP, although there are some caveats currently
- Can retreive light data in shader graph and regular forward mode shaders, meaning you can have more active lights in the scene at any given time
- Uses same lighting calculation as the regular URP shaders, so it follows the same style
- There is the start of a material converter
- Light culling based on camera frustum

# Usage
Currently tested in 2020.1 with URP 8.2

1. Add files from repo to your project
2. Add the Deferred Feature to your URP renderer asset
3. Rendering Paths:  
    Deferred Pass:  
        1. Enable "Deferred Pass" in the renderer feature settings  
        2. Convert/Change the material type from URP Lit to the Deferred Lit shader you get with the project  
        3. It should now be lit by the deferred pass  
    Forward+ Pass:  
        1. Access light data as show in the example graph  
        2. Nothing else needed, although you can probaby disable "Deferred Pass" in the renderer feature settings for performance reasons.  
4. Add GameObjects to your scene and attachs the DeferredLightsData component to them
5. Lights should now interact with your objects

There are a few example scenes included as well.

Do note that with the current content of this package the material variety for deferred pass is severly limited.

# Changes
## V0.2.0
- More optimized GBuffer pass using MRT(Multiple Render Targets)
    Support for MRT is limited to DX11+, OpenGL 3.2+, OpenGL ES 3+, Metal, Vulkan, PS4/XB1 and can be toggled in the Render Feature settings
    Should improve the overhead on complex scenes drastically, since we only need to run one pass for GBuffer
- Support for Normal/Bump maps
- General cleanup of code base