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

Open the scenes / prefabs with lost references, and drag the appropriate script on there

Credits:
A massive thank you to Anton Hand of rust ltd for helping to make this system the best it could be!
The FX1 Microstar is kindly loaned from HEDRON central requisitions


Release notes:

1.4.1

-Optimized instantiating systems ~4-5x. Generates ~20x less garbage now
-Optimized update of systems by being more careful about what properties to bind when
-Added a MaxSpeed property to particles


-Fixed an issue where enabling / disabling a system would still interpolate the positions.
-Fixed extension templates, removed explicit bind call, handled when dispatched now
-Cleaned up some code

NOTE: You need to update your extensions, as the system parameters have changed