using System;
using UnityEngine;

namespace KSPCSS
{
    public class AnimationModule : PartModule
    {
        //Name of animation clip
        [KSPField]
        public string clipName;

        //Activation keycode http://docs.unity3d.com/Documentation/ScriptReference/KeyCode.html
        [KSPField]
        public string key;

        //Speed of animation
        [KSPField]
        public float speed;

        //Amount of resource required to complete animation
        [KSPField]
        public float resourceAmount;

        //Resource used when activating animation
        [KSPField]
        public string resource;

        Animation anim;
        bool play = false;

        public override void OnStart(PartModule.StartState state)
        {
            anim = part.FindModelAnimators(clipName)[0];
            anim[clipName].wrapMode = WrapMode.ClampForever;
            //anim.stop() will cause the animation to not update when manually updating state.time
            //Stop anim without calling stop()
            anim[clipName].speed = 0; 
            //We negate speed on keypress. Ensure playing forward on first keypress
            speed *= -1;
        }
        public override void OnUpdate()
        {
            AnimationState state = anim[clipName];
            KeyCode inputKey;
            //Parse config value into KeyCode
            inputKey = (KeyCode)Enum.Parse(typeof(KeyCode), key);

            //Play animation. Each time invert direction
            if (Input.GetKeyDown(inputKey))
            {
                speed *= -1;
                play = true;
            }
            //Animation is active
            if (play)
            {
                //Amount of resource required to play animation at full speed
                float requiredResource = (Time.deltaTime / state.length) * resourceAmount;

                //Ratio of required resource : acquired resource
                float ratioFufilled = part.RequestResource(resource, requiredResource) / requiredResource;
                state.time += speed * Time.deltaTime * ratioFufilled;
            }
            //If animation passed start/end, move to start/end and stop anim
            float normalizedTime = anim[clipName].normalizedTime;
            if (normalizedTime >= 1 || normalizedTime <= 0)
            {
                anim[clipName].normalizedTime = (float)Math.Round(normalizedTime);
                play = false;
            }
            base.OnUpdate();
        }
    }
}
