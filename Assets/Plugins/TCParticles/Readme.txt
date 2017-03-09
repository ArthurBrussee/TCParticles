Thank you for purchasing TC Particles! I hope you are just as enthused by the possibilities of GPU particles as I am!

For more info, support, documentation and the API reference, please refer to:
tcparticles.tinycubestudio.com

Readme: http://tcparticles.tinycubestudio.com/?page_id=131 
Documentation: http://tcparticles.tinycubestudio.com/?page_id=10
API Reference: http://tcparticles.tinycubestudio.com/?page_id=11


IMPORTANT: If you upgraded from a version before 1.4 and had a DLL version you WILL lose your references. To fix:

1. Delete all files in the implementation folder.
2. Extract the TC Particles DLL, and place the DLL in the implementation folder

OR

Open the scenes / prefabs with lost references, and drag the appropriate component on there


Credits:
A massive thank you to Anton Hand of rust ltd for helping to make this system the best it could be!
The FX1 Microstar is kindly loaned from HEDRON central requisitions


Release notes:

1.6

Particles now store their color!
	-You can now emit particles from a textured mesh with uv1/2/3/4 channel
	-Colors can also be set with new 'ParticleProto' emission (see API) 
	-Color over lifetime is multiplicative with this base color 
	-Stored in compact format, two other fields were optimised away, so particles are smaller than before! (few % performance increase)

TC Force turbulence generation rewritten:
	-Parameters can now be adjusted in real time! Automatically updates if needed, takes <1ms in nearly all cases. 
	-No more hassle to manage textures in the project etc.
	-Now uses 'curl' noise internally - this noise has a nice property (divergence free) that prevents clumps of particles & looks very fluid like
	-Should match old settings mostly. If it doesn't - switch force to TurbulenceTexture mode and old texture will still be plugged in.
	-Don't need power of 2 resolution anymore, can support higher resolutions than before
	-New 'persistence' parameter on most noise types for mroe control
	-Turbulence preview is much improved - now runs all on the GPU and was made very fast, colors show proper gradient instead of some made up derivative, now displaces arrows in direction of force for some additional feedback of the noise 'feel'
	-Renamed Pink noise to Perlin noise (since that's always what it was)
	-Voronoi was removed as it never worked super well, didn't port well to GPU
	-The Turbulence Frequency & power was removed (an additional noise on top of the regular one). The new noises do well without + the new persistence parameter helps.

Rewrote parts of mesh emission  
	-Made NormalizeArea explicit instead of guessing it - faster if off, but particles may appear non uniform on the surface. On by default now as NormalizeArea was made MUCH faster too (binary instead of linear search).
	-Fixed wireframe not updating correctly when changing meshes 
	-Fixed a few memory leaks of the mesh buffers 

New ParticleProto API: Emit from a list of 'particle prototypes' that can set initial position, size, velocity, color 

Improved extension system:
	-Now sets more parameters on extension kernels
	-Recognizes numthreads and scales appropriately
	-New TCFramework.cginc means you don't have to copy code anymore to extension compute shaders & will be in sync 
	
Colour over lifetime (& velocity over lifetime) is now basked to 128 sized gradients (was 64), little nicer!
	
Performance:
	-Removed GC generated every frame by the renderer
	-Removed GC when frustum culling was enabled
	-Performance improvments in emission, forces & colliders (few %)
	-Fixed emission launching 2x the nr of threads actually needed
	-More buffer reuse & other micro optimizations

Large code cleanup, small bug fixes
	-Removed BurstsForSystem API on shape emitters - was confusing and didn't really work
	-Removed some unused files
	-Fixed particles sometimes spawning outside of sphere/hemisphere shape 
	-Fixed random emission direction besides having a ranom direction also having a random speed and not using the start speed
	
Samples:
	-New point cloud importer & Sample data! Shows rendering >3.5 million points 
	-Added new ColorCube sampel showing ParticleProto sample 
	-Forces sample was revamped, few bug fixes, nicer behaviour.
	-Modernised old samples, cleaned, restructed folders to group each sample in own folder. 



1.5

-Moved the Editor folder now that it can be under the TC Particles root to make package managment easier


-Improved force priority sorting to sort by actual force not just distance
-Improved performance of priority sorting when a system is linked to more forces/colliders than max forces/colliders
-Reduced garbage generated in a bunch of scenarios
-Increased performance for multi pass materials


-Fixed tail UV globally being set to the tail UV of the very first TC Particle system created
    NOTE: The value has been globally reset to the default 0.5 since it wasn't doing anything
-Fixed all compile warnings
-Fixed missing gizmos and wireframes
-Fixed some inconsitencies between constant / non constant forces
-Fixed velocity over lifetime not affecting velocity effects like stretch or color over velocity
-Fixed some issues with in editor visualisation
-Fixed some nullrefs on starting / stopping
-Fixed Vortex inward force framerate dependance issue
-Fixed playback speed + "sticky" particle issues 
-Fixed issue where sometimes colliders/forces were picked up only after ~a second 


1.4.1

-Optimized instantiating systems ~4-5x. Generates ~20x less garbage now
-Optimized update of systems by being more careful about what properties to bind when
-Added a MaxSpeed property to particles


-Fixed an issue where enabling / disabling a system would still interpolated the positions.
-Fixed extension templates, removed explicit bind call, handled when dispatched now
-Cleaned up some code

NOTE: You need to update your extensions, as the system parameters have changed