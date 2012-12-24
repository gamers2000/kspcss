Example animation module usage : 

MODULE
{
	name = AnimationModule
	clipName = CargoDoors1 
	key = Alpha0 
	resource = ElectricCharge
	resourceAmount = 5
	speed = 1
}

name 		: Will change in the future - leave as is for now
clipName	: Name of animation clip in unity
key 		: Activation keycode, find in http://docs.unity3d.com/Documentation/ScriptReference/KeyCode.html
resource	: Name of resource required to activate animation
resourceAmount	: Amount of resource required to complete animation once
speed		: Speed to run animation at. 2 for double speed etc. resourceAmount is NOT affected by this