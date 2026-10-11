using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HangGliderController : MonoBehaviour
{
	[Header("Flight")]
	public float launchSpeed = 14f;
	public float minSpeed = 10f;
	public float maxSpeed = 32f;
	public float acceleration = 5f;

	[Header("Gliding")]
	public float liftStrength = 18f;
	public float glideGravity = 0.65f;
	public float climbStrength = 12f;
	public float diveStrength = 10f;
	public float airResistance = 0.3f;

	[Header("Steering")]
	public float pitchSpeed = 65f;
	public float rollSpeed = 100f;
	public float yawSpeed = 55f;
	public float turnLift = 8f;
	public float rotationSmooth = 4f;

	[Header("Controls")]
	public bool useInput = true;

	[Header("Runtime")]
	public float speed;
	public float speedKmh;
	public float verticalSpeed;
	public float bankAngle;

	private Rigidbody rb;

	private float pitchInput;
	private float rollInput;
	private float yawInput;

	private float pitch;
	private float bank;
	private float heading;


	[Header("Auto Climb")]
	public bool autoClimbOnTrigger = true;
	private bool triggerForcingClimb;
	private int triggerContacts;

	[Header("Wind Audio")]
	public AudioSource windSource;
	public float minPitch = 0.8f;
	public float maxPitch = 1.5f;
	public float minVolume = 0.1f;
	public float maxVolume = 1f;
	public float windAudioSmooth = 2f;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();

		rb.mass = 40f;
		rb.useGravity = true;
		rb.linearDamping = 0f;
		rb.angularDamping = 2f;
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		rb.collisionDetectionMode =
			CollisionDetectionMode.ContinuousDynamic;

		rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
	}

	private void Start()
	{
		Vector3 euler = transform.eulerAngles;

		pitch = NormalizeAngle(euler.x);
		heading = euler.y;
		bank = 0f;

		rb.linearVelocity = transform.forward * launchSpeed;
	}

	private void Update()
	{
		if (useInput)
		{
			pitchInput = 0f;

			if (Input.GetKey(KeyCode.W))
				pitchInput = -1f;

			if (Input.GetKey(KeyCode.S))
				pitchInput = 1f;

			rollInput = Input.GetAxisRaw("Horizontal");

			yawInput = 0f;

			if (Input.GetKey(KeyCode.Q))
				yawInput = -1f;

			if (Input.GetKey(KeyCode.E))
				yawInput = 1f;
		}

		// O trigger tem prioridade sobre o teclado.
		if (autoClimbOnTrigger && triggerContacts > 0)
		{
			pitchInput = 1f;
		}
	}

	private void FixedUpdate()
	{
		float dt = Time.fixedDeltaTime;

		speed = rb.linearVelocity.magnitude;
		speedKmh = speed * 3.6f;
		verticalSpeed = rb.linearVelocity.y;

		// A/D inclinam a asa.
		float targetBank = -rollInput * 55f;

		bank = Mathf.Lerp(
			bank,
			targetBank,
			rotationSmooth * dt
		);

		// W/S mudam a inclinação do nariz.
		pitch += -pitchInput * pitchSpeed * dt;
		pitch = Mathf.Clamp(pitch, -35f, 35f);

		// Inclinar também produz uma curva.
		float turnFactor = Mathf.Clamp(
			speed / 16f,
			0.4f,
			1.8f
		);

		heading += (
			-Mathf.Sin(bank * Mathf.Deg2Rad) *
			yawSpeed * turnFactor +
			yawInput * yawSpeed * 0.5f
		) * dt;

		Quaternion targetRotation = Quaternion.Euler(
			pitch,
			heading,
			bank
		);

		rb.MoveRotation(
			Quaternion.Slerp(
				rb.rotation,
				targetRotation,
				rotationSmooth * dt
			)
		);

		// A velocidade cresce na descida e diminui na subida.
		float verticalFactor = -verticalSpeed;

		float speedChange = verticalFactor * acceleration * 0.12f;

		speed += speedChange * dt;

		speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

		// Mantém impulso para frente.
		Vector3 forwardVelocity = transform.forward * speed;

		rb.AddForce(
			(forwardVelocity - rb.linearVelocity) * acceleration,
			ForceMode.Acceleration
		);

		// Sustentação arcade.
		float noseFactor = Mathf.Clamp(
			1f + pitch / 90f,
			0.45f,
			1.3f
		);

		float lift = liftStrength * noseFactor;

		// Perda de sustentação durante curvas inclinadas.
		float bankingLoss =
			Mathf.Abs(Mathf.Sin(bank * Mathf.Deg2Rad)) *
			turnLift;

		float verticalForce =
			lift -
			Physics.gravity.magnitude * glideGravity -
			bankingLoss;

		// Puxar o nariz para cima dá impulso vertical.
		verticalForce += pitchInput * climbStrength;

		// Apontar para baixo ajuda a mergulhar e acelerar.
		if (pitchInput < 0f)
			verticalForce += diveStrength * -pitchInput;

		rb.AddForce(
			Vector3.up * verticalForce,
			ForceMode.Acceleration
		);

		// Resistência suave do ar.
		rb.AddForce(
			-rb.linearVelocity * airResistance,
			ForceMode.Acceleration
		);

		// Áudio do vento controlado pela velocidade.
		if (windSource != null)
		{
			float speedRatio = Mathf.Clamp01(speed / maxSpeed);

			float targetAudioPitch = Mathf.Lerp(
				minPitch,
				maxPitch,
				speedRatio
			);

			float targetVolume = Mathf.Lerp(
				minVolume,
				maxVolume,
				speedRatio
			);

			windSource.pitch = Mathf.Lerp(
				windSource.pitch,
				targetAudioPitch,
				windAudioSmooth * dt
			);

			windSource.volume = Mathf.Lerp(
				windSource.volume,
				targetVolume,
				windAudioSmooth * dt
			);
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!autoClimbOnTrigger)
			return;

		triggerContacts++;

		Debug.Log(
			$"[HangGlider] Trigger tocou em: {other.name}. " +
			$"Contatos: {triggerContacts}"
		);
	}

	private void OnTriggerExit(Collider other)
	{
		if (!autoClimbOnTrigger)
			return;

		triggerContacts = Mathf.Max(0, triggerContacts - 1);

		Debug.Log(
			$"[HangGlider] Trigger saiu de: {other.name}. " +
			$"Contatos: {triggerContacts}"
		);
	}

	public void SetFlightInput(
		float pitchValue,
		float rollValue,
		float yawValue = 0f)
	{
		pitchInput = Mathf.Clamp(pitchValue, -1f, 1f);
		rollInput = Mathf.Clamp(rollValue, -1f, 1f);
		yawInput = Mathf.Clamp(yawValue, -1f, 1f);
	}

	public void SetInputEnabled(bool enabled)
	{
		useInput = enabled;

		if (!enabled)
		{
			pitchInput = 0f;
			rollInput = 0f;
			yawInput = 0f;
		}
	}

	private float NormalizeAngle(float angle)
	{
		while (angle > 180f)
			angle -= 360f;

		while (angle < -180f)
			angle += 360f;

		return angle;
	}
}