
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LunetaEffect : MonoBehaviour
{
	[Header("Configurações")]
	[SerializeField, Min(0.01f)] private float intervalo = 1f;
	[SerializeField] private Volume volume;

	[Header("Valores LIGADO")]
	[SerializeField, Range(-1f, 1f)] private float lensDistortionLigado = 0.5f;
	[SerializeField, Min(1f)] private float focalLengthLigado = 50f;
	[SerializeField, Range(0f, 1f)] private float vignetteLigado = 0.75f;

	[Header("Valores DESLIGADO")]
	[SerializeField, Range(-1f, 1f)] private float lensDistortionDesligado = 0f;
	[SerializeField, Min(1f)] private float focalLengthDesligado = 50f;
	[SerializeField, Range(0f, 1f)] private float vignetteDesligado = 0f;

	private LensDistortion lensDistortion;
	private DepthOfField depthOfField;
	private Vignette vignette;

	private bool estaLigada;
	private bool transicionando;
	private float timer;

	private float startLensDistortion;
	private float startFocalLength;
	private float startVignette;

	public GameObject playerCamera;
	public bool insideArea;

	public UnityEvent OnStartEffect, OnStopEffect;

	private void Start()
	{
		if (volume == null || volume.profile == null)
		{
			Debug.LogError(
				"LunetaEffect: atribua um Volume com Profile no Inspector.",
				this
			);
			enabled = false;
			return;
		}

		VolumeProfile profile = volume.profile;

		if (!profile.TryGet(out lensDistortion) ||
			!profile.TryGet(out depthOfField) ||
			!profile.TryGet(out vignette))
		{
			Debug.LogError(
				"LunetaEffect: adicione Lens Distortion, Depth Of Field " +
				"e Vignette ao Volume Profile.",
				this
			);
			enabled = false;
			return;
		}

		lensDistortion.intensity.overrideState = true;
		depthOfField.focalLength.overrideState = true;
		depthOfField.mode.overrideState = true;
		vignette.intensity.overrideState = true;

		// O focalLength só é usado no modo Bokeh.
		depthOfField.mode.value = DepthOfFieldMode.Bokeh;

		AplicarValoresDesligado();
	}

	private void Update()
	{
		if (insideArea)
		{
			if (Input.GetMouseButtonDown(0) && !transicionando)
			{
				ToggleLuneta();
			}
		}

		if (!transicionando)
			return;

		timer += Time.deltaTime;
		float t = Mathf.Clamp01(timer / intervalo);

		// Suaviza a entrada e a saída da transição.
		float suavizado = Mathf.SmoothStep(0f, 1f, t);

		float alvoLens = estaLigada
			? lensDistortionLigado
			: lensDistortionDesligado;

		float alvoFocal = estaLigada
			? focalLengthLigado
			: focalLengthDesligado;

		float alvoVignette = estaLigada
			? vignetteLigado
			: vignetteDesligado;

		lensDistortion.intensity.value = Mathf.Lerp(
			startLensDistortion, alvoLens, suavizado);

		depthOfField.focalLength.value = Mathf.Lerp(
			startFocalLength, alvoFocal, suavizado);

		vignette.intensity.value = Mathf.Lerp(
			startVignette, alvoVignette, suavizado);

		if (t >= 1f)
		{
			transicionando = false;
			timer = 0f;
		}
	}

	public void ToggleLuneta()
	{
		if (transicionando) return;

		startLensDistortion = lensDistortion.intensity.value;
		startFocalLength = depthOfField.focalLength.value;
		startVignette = vignette.intensity.value;

		estaLigada = !estaLigada;
		transicionando = true;
		timer = 0f;

		insideArea = estaLigada;
		playerCamera.gameObject.SetActive(!estaLigada);
		if (estaLigada)
		{			
			OnStartEffect.Invoke();
		}
		else
		{
			OnStopEffect.Invoke();
		}

		Debug.Log($"Luneta {(estaLigada ? "LIGADA" : "DESLIGADA")}");
	}

	public void SetLuneta(bool state)
	{
		if (transicionando) return;

		startLensDistortion = lensDistortion.intensity.value;
		startFocalLength = depthOfField.focalLength.value;
		startVignette = vignette.intensity.value;

		estaLigada = state;
		transicionando = true;
		timer = 0f;

		insideArea = estaLigada;
		playerCamera.gameObject.SetActive(!estaLigada);

		if (estaLigada)
		{			
			OnStartEffect.Invoke();
		}
		else
		{
			OnStopEffect.Invoke();
		}

		Debug.Log($"Luneta {(estaLigada ? "LIGADA" : "DESLIGADA")}");
	}

	private void AplicarValoresDesligado()
	{
		lensDistortion.intensity.value = lensDistortionDesligado;
		depthOfField.focalLength.value = focalLengthDesligado;
		vignette.intensity.value = vignetteDesligado;
	}
}
