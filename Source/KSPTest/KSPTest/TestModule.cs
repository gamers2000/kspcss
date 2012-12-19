using System;
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

        Animation[] clips;

        int count = 0;

        public override void OnStart(PartModule.StartState state)
        {
            //Gets all the animation clips 
            clips = part.FindModelAnimators(clipName);
            foreach (Animation anim in clips)
            {
                anim[clipName].wrapMode = WrapMode.ClampForever;
                anim.Stop();
            }
        }
        //God damnit I had this working before trying to read config values
        public override void OnUpdate()
        {
            KeyCode inputKey;
            inputKey = (KeyCode)Enum.Parse(typeof(KeyCode),key);
                       
            if (Input.GetKeyDown(inputKey))
            {
                PDebug.Log(count + "Keydown");
                ++count;
                foreach (Animation anim in clips)
                {
                    AnimationState state = anim[clipName];
                    state.speed *= -1;
                    anim.Play(clipName);
                }
            }
            base.OnUpdate();
        }
    }
}
