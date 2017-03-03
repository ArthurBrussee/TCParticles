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