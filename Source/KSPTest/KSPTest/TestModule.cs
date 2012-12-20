﻿using System;
using UnityEngine;

namespace KSPTest
{
    public class TestModule : PartModule
    {
        //Name of animation clip
        [KSPField]
        public string clipName;

        //Activation keycode http://docs.unity3d.com/Documentation/ScriptReference/KeyCode.html
        [KSPField]
        public string key;

        Animation anim;

        public override void OnStart(PartModule.StartState state)
        {
            anim = part.FindModelAnimators(clipName)[0];
            anim[clipName].wrapMode = WrapMode.ClampForever;
            //Ensure speed is positive on first playing
            anim[clipName].speed = -1;
            anim.Stop();
        }
        public override void OnUpdate()
        {
            KeyCode inputKey;
            //Parse config value into KeyCode
            inputKey = (KeyCode)Enum.Parse(typeof(KeyCode), key);

            //<rant>Clampmode doesn't stop at time 1, it just runs through</rant>
            //If overrun length of animation, reset to start/end
            float time = anim[clipName].normalizedTime;
            PDebug.Log("prev time" + time);
            if (time >= 1 || time <= 0)
                anim[clipName].normalizedTime = (float)Math.Round(time);
            PDebug.Log((int)time);
            //Play animation. Each time invert direction
            if (Input.GetKeyDown(inputKey))
            {
                    AnimationState state = anim[clipName];
                    state.speed *= -1;
                    anim.Play(clipName);
            }
            base.OnUpdate();
        }
    }
}
