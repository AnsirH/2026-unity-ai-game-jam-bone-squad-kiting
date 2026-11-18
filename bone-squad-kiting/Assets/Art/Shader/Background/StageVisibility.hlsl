#ifndef BONE_SQUAD_VISIBILITY_INCLUDED
#define BONE_SQUAD_VISIBILITY_INCLUDED

half StageBrightness(half visibility, half darkness)
{
    half brightness = 1.0h - darkness * (1.0h - visibility);
#if !defined(UNITY_COLORSPACE_GAMMA)
    brightness *= brightness;
#endif
    return brightness;
}

#endif
