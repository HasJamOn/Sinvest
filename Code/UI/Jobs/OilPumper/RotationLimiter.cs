using Sandbox;
using System;

namespace Sinvest;

[Group( "Physics" )]
public sealed class RotationLimiter : Component
{
	[Property] public float MaxYaw { get; set; } = 15f;
	
	[Header("Governing")]
	[Property] public float MaxDegreesPerSecond { get; set; } = 30f; 
	[Property] public float FeatherZone { get; set; } = 0.2f; // Smaller zone for a "crisper" stop
	
	[Header("Resistive Settings")]
	[Property] public float BrakeStrength { get; set; } = 15.0f; // Resistance when entering the feather zone

	private Angles _initialAngles;
	private Rigidbody _rb;

	protected override void OnStart()
	{
		_initialAngles = LocalRotation.Angles();
		_rb = Components.Get<Rigidbody>();

		if ( _rb.IsValid() )
		{
			// Keep damping moderate so it doesn't spin forever, 
			// but low enough to feel "free"
			_rb.AngularDamping = 1.0f; 
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !_rb.IsValid() ) return;

		var currentAngles = LocalRotation.Angles();
		float diffYaw = Angles.NormalizeAngle( currentAngles.yaw - _initialAngles.yaw );
		float absDiff = MathF.Abs( diffYaw );
		float limitStart = MaxYaw * (1.0f - FeatherZone);

		// 1. THE GOVERNOR (Speed Limit)
		float maxRad = MaxDegreesPerSecond.DegreeToRadian();
		if ( MathF.Abs( _rb.AngularVelocity.z ) > maxRad )
		{
			_rb.AngularVelocity = _rb.AngularVelocity.WithZ( MathF.Sign( _rb.AngularVelocity.z ) * maxRad );
		}

		// 2. THE BRAKE (Feathering without Pushback)
		if ( absDiff > limitStart )
		{
			// Only apply resistance if we are moving TOWARDS the limit
			bool movingOutward = (diffYaw > 0 && _rb.AngularVelocity.z > 0) || (diffYaw < 0 && _rb.AngularVelocity.z < 0);

			if ( movingOutward )
			{
				float t = (absDiff - limitStart) / (MaxYaw - limitStart);
				t = Math.Clamp( t, 0, 1 );

				// Apply a counter-torque that only slows the object down, never pushes it back
				float brakeTorque = -_rb.AngularVelocity.z * BrakeStrength * t;
				_rb.ApplyTorque( new Vector3( 0, 0, brakeTorque ) );
			}
		}

		// 3. THE HARD STOP (Safety Net)
		if ( absDiff >= MaxYaw )
		{
			// Verify if we are still trying to move past the limit
			if ( (diffYaw > 0 && _rb.AngularVelocity.z > 0) || (diffYaw < 0 && _rb.AngularVelocity.z < 0) )
			{
				_rb.AngularVelocity = _rb.AngularVelocity.WithZ( 0 );
			}

			// Teleport back to exact limit if breached to prevent creeping
			float clampedYaw = Math.Clamp( diffYaw, -MaxYaw, MaxYaw );
			LocalRotation = Rotation.From( _initialAngles.WithYaw( _initialAngles.yaw + clampedYaw ) );
		}
	}
}
