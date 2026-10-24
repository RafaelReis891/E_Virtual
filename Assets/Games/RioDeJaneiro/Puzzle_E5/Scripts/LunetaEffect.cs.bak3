
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

	[Header("Imagem desta luneta")]
	[SerializeField] private UnityEngine.UI.Image imagemUI; // arraste o objeto "Imagem" do Canvas
	[SerializeField] private Sprite imagemDaLuneta;        // a foto desta luneta

	[Header("Mascara (overlay do Canvas)")]
	[Tooltip("Objeto Luneta do Canvas. E desligado ao sair, mesmo no meio da transicao.")]
	[SerializeField] private GameObject overlayMascara;
	[Tooltip("Tamanho do buraco: 1 = original, maior = buraco maior")]
	[SerializeField, Range(0.3f, 3f)] private float tamanhoMascara = 1f;

	private void AplicarTamanhoMascara()
	{
		if (overlayMascara != null) overlayMascara.transform.localScale = Vector3.one * tamanhoMascara;
	}

	public UnityEvent OnStartEffect, OnStopEffect;

	[Header("Sair com ESC")]
	[Tooltip("Componente TimedEvent que mostra a mascara apos 1s. Ao sair (ESC ou trigger) o timer e cancelado.")]
	[SerializeField] private MonoBehaviour timedEventParaCancelar;
	[SerializeField] private KeyCode teclaSair = KeyCode.Escape;
	// true depois de sair por ESC: bloqueia religar ate o jogador sair do trigger
	private bool saiuPorEsc;

	private void CancelarTimedEvent()
	{
		if (timedEventParaCancelar != null) timedEventParaCancelar.StopAllCoroutines();
	}

	private void LateUpdate()
	{
		// depois do ESC, garante que nada religue a mascara enquanto o jogador ainda esta no trigger
		if (saiuPorEsc && !estaLigada && overlayMascara != null && overlayMascara.activeSelf)
			overlayMascara.SetActive(false);
	}

	private void Start()
	{
		// se nao foi arrastado no Inspector, procura um TimedEvent neste mesmo objeto
		if (timedEventParaCancelar == null)
		{
			foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
				if (mb != null && mb != this && mb.GetType().Name == "TimedEvent") { timedEventParaCancelar = mb; break; }
		}

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
		if (estaLigada && Input.GetKeyDown(teclaSair))
		{
			SetLuneta(false);
			saiuPorEsc = true;
			return;
		}

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
		if (transicionando || lensDistortion == null) return;

		startLensDistortion = lensDistortion.intensity.value;
		startFocalLength = depthOfField.focalLength.value;
		startVignette = vignette.intensity.value;

		estaLigada = !estaLigada;
		transicionando = true;
		timer = 0f;

		insideArea = estaLigada;
		if (playerCamera != null) playerCamera.SetActive(!estaLigada);
		if (!estaLigada) CancelarTimedEvent();
		if (estaLigada)
		{			
			AplicarImagem();
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
		if (!state)
		{
			// sempre restaura, mesmo se o script estiver desativado
			CancelarTimedEvent();
			if (overlayMascara != null) overlayMascara.SetActive(false);
			if (playerCamera != null) playerCamera.SetActive(true);
			// chamada de saida enquanto ja desligada (ex.: saiu do trigger apos ESC) libera nova entrada
			if (!estaLigada) { saiuPorEsc = false; insideArea = false; return; }
		}
		else if (saiuPorEsc) return; // saiu por ESC: precisa sair do trigger antes
		if (!enabled || lensDistortion == null) return;
		// permite sair mesmo no meio da transicao de entrada
		if (transicionando && state == estaLigada) return;
		if (overlayMascara != null && !state) overlayMascara.SetActive(false);
		AplicarTamanhoMascara();

		startLensDistortion = lensDistortion.intensity.value;
		startFocalLength = depthOfField.focalLength.value;
		startVignette = vignette.intensity.value;

		estaLigada = state;
		transicionando = true;
		timer = 0f;

		insideArea = estaLigada;
		if (playerCamera != null) playerCamera.SetActive(!estaLigada);

		if (estaLigada)
		{			
			AplicarImagem();
			OnStartEffect.Invoke();
		}
		else
		{
			OnStopEffect.Invoke();
		}

		Debug.Log($"Luneta {(estaLigada ? "LIGADA" : "DESLIGADA")}");
	}

	private void AplicarImagem()
	{
		if (imagemUI != null && imagemDaLuneta != null)
			imagemUI.sprite = imagemDaLuneta;
	}

	private void AplicarValoresDesligado()
	{
		lensDistortion.intensity.value = lensDistortionDesligado;
		depthOfField.focalLength.value = focalLengthDesligado;
		vignette.intensity.value = vignetteDesligado;
	}
}
