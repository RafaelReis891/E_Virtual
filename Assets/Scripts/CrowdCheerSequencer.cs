using UnityEngine;

/// <summary>
/// Periodicamente interrompe o idle do URPUnlitTextureSwitcher, toca uma
/// sequencia de cheer (Frame 5, 6, 6, 7) no mesmo MaterialPropertyBlock
/// (_BaseMap) e devolve o controle ao loop idle.
///
/// Uso no Inspector (teste em Teste_Torcida, sem substituir os originais):
/// - No URPUnlitTextureSwitcher, Textures = CrowdTeste_Frame1..4 (idle).
///   Nao coloque 5-7 nessa lista.
/// - Neste componente, Cheer Textures = CrowdTeste_Frame5, 6 e 7.
/// - Cheer Sequence padrao {0,1,1,2} = frames 5, 6 (segurado), 7.
/// - Opcional: CrowdBlendShapeAnimator no mesmo objeto; durante o cheer o
///   sequencer desliga o animator e sobe o blendshape "03 Up", depois devolve.
/// WebGL: apenas SetTexture no MPB e SetBlendShapeWeight.
/// </summary>
[DisallowMultipleComponent]
public class CrowdCheerSequencer : MonoBehaviour
{
	[Header("Dependencias")]
	[Tooltip("Switcher de idle (Frame 1-4). Se vazio, procura no mesmo objeto.")]
	[SerializeField] private URPUnlitTextureSwitcher idleSwitcher;

	[Tooltip("Animator de blendshapes. Se vazio, procura no mesmo objeto.")]
	[SerializeField] private CrowdBlendShapeAnimator blendAnimator;

	[SerializeField] private Renderer targetRenderer;
	[SerializeField] private int materialIndex;

	[Header("URP Unlit")]
	[SerializeField] private string textureProperty = "_BaseMap";

	[Header("Cheer (Frame 5, 6, 7)")]
	[Tooltip("Texturas de cheer, na ordem Frame 5, 6, 7.")]
	[SerializeField] private Texture[] cheerTextures;

	[Tooltip("Indices em Cheer Textures. Padrao: 5, 6 (segurado), 6, 7.")]
	[SerializeField] private int[] cheerSequence = new int[] { 0, 1, 1, 2 };

	[Min(0.01f)]
	[SerializeField] private float cheerFramesPerSecond = 6f;

	[Header("Intervalo entre cheers")]
	[Min(0.1f)]
	[SerializeField] private float minInterval = 4f;

	[Min(0.1f)]
	[SerializeField] private float maxInterval = 10f;

	[SerializeField] private bool playOnStart = true;

	[Header("BlendShape opcional")]
	[SerializeField] private bool pushBlendShape = true;

	[SerializeField] private string cheerBlendShapeName = "03 Up";

	[Range(0f, 100f)]
	[SerializeField] private float cheerBlendWeight = 80f;

	[Min(0.01f)]
	[SerializeField] private float blendLerpSpeed = 6f;

	private MaterialPropertyBlock propertyBlock;
	private int texturePropertyId;
	private int blendShapeIndex = -1;
	private bool savedAnimatorEnabled;

	private enum State
	{
		Waiting,
		Cheering,
		Recovering
	}

	private State state = State.Waiting;
	private float waitTimer;
	private int sequenceIndex;
	private float frameTimer;
	private float currentBlend;
	private bool running;

	private void Awake()
	{
		if (idleSwitcher == null)
			idleSwitcher = GetComponent<URPUnlitTextureSwitcher>();

		if (blendAnimator == null)
			blendAnimator = GetComponent<CrowdBlendShapeAnimator>();

		if (targetRenderer == null)
		{
			if (idleSwitcher != null)
				targetRenderer = idleSwitcher.GetComponent<Renderer>();

			if (targetRenderer == null)
				targetRenderer = GetComponent<Renderer>();
		}

		propertyBlock = new MaterialPropertyBlock();
		texturePropertyId = Shader.PropertyToID(textureProperty);

		ResolveBlendShapeIndex();
	}

	private void Start()
	{
		running = playOnStart;
		ScheduleNextCheer();
	}

	private void Update()
	{
		if (!running)
			return;

		float dt = Time.deltaTime;

		if (state == State.Waiting)
		{
			waitTimer -= dt;
			if (waitTimer <= 0f)
				BeginCheer();
			return;
		}

		if (state == State.Cheering)
		{
			TickBlend(dt, cheerBlendWeight);

			if (cheerTextures == null || cheerTextures.Length == 0)
			{
				EndCheer();
				return;
			}

			frameTimer += dt;
			float frameDuration = 1f / cheerFramesPerSecond;

			if (frameTimer >= frameDuration)
			{
				frameTimer -= frameDuration;
				sequenceIndex++;

				if (cheerSequence == null || sequenceIndex >= cheerSequence.Length)
				{
					EndCheer();
					return;
				}

				ApplyCheerFrame(sequenceIndex);
			}

			return;
		}

		if (state == State.Recovering)
		{
			TickBlend(dt, 0f);

			if (currentBlend <= 0.5f)
				FinishRecover();
		}
	}

	public void Play()
	{
		running = true;
	}

	public void Stop()
	{
		if (state != State.Waiting)
			FinishRecover();

		running = false;
	}

	private void ScheduleNextCheer()
	{
		state = State.Waiting;
		float min = Mathf.Min(minInterval, maxInterval);
		float max = Mathf.Max(minInterval, maxInterval);
		waitTimer = Random.Range(min, max);
	}

	private void BeginCheer()
	{
		if (cheerTextures == null || cheerTextures.Length == 0)
		{
			ScheduleNextCheer();
			return;
		}

		state = State.Cheering;
		sequenceIndex = 0;
		frameTimer = 0f;

		if (idleSwitcher != null)
			idleSwitcher.Stop();

		if (pushBlendShape && blendAnimator != null)
		{
			savedAnimatorEnabled = blendAnimator.enabled;
			blendAnimator.enabled = false;
		}

		ApplyCheerFrame(0);
	}

	private void EndCheer()
	{
		if (pushBlendShape && blendShapeIndex >= 0 && currentBlend > 0.5f)
		{
			state = State.Recovering;
			return;
		}

		FinishRecover();
	}

	private void FinishRecover()
	{
		if (pushBlendShape)
			SetBlend(0f);

		if (blendAnimator != null && pushBlendShape)
			blendAnimator.enabled = savedAnimatorEnabled;

		if (idleSwitcher != null)
		{
			idleSwitcher.SetFrame(0);
			idleSwitcher.Play();
		}

		ScheduleNextCheer();
	}

	private void ApplyCheerFrame(int sequencePos)
	{
		if (targetRenderer == null || cheerTextures == null)
			return;

		if (cheerSequence == null || sequencePos < 0 || sequencePos >= cheerSequence.Length)
			return;

		int texIndex = cheerSequence[sequencePos];
		if (texIndex < 0 || texIndex >= cheerTextures.Length)
			return;

		Texture tex = cheerTextures[texIndex];
		if (tex == null)
			return;

		targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
		propertyBlock.SetTexture(texturePropertyId, tex);
		targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
	}

	private void ResolveBlendShapeIndex()
	{
		blendShapeIndex = -1;

		SkinnedMeshRenderer skinned = targetRenderer as SkinnedMeshRenderer;
		if (skinned == null)
			skinned = GetComponentInChildren<SkinnedMeshRenderer>();

		if (skinned == null || skinned.sharedMesh == null)
			return;

		if (string.IsNullOrEmpty(cheerBlendShapeName))
			return;

		blendShapeIndex = skinned.sharedMesh.GetBlendShapeIndex(cheerBlendShapeName);
	}

	private void TickBlend(float dt, float target)
	{
		if (!pushBlendShape || blendShapeIndex < 0)
			return;

		currentBlend = Mathf.MoveTowards(
			currentBlend,
			target,
			blendLerpSpeed * 20f * dt
		);

		SetBlend(currentBlend);
	}

	private void SetBlend(float weight)
	{
		currentBlend = weight;

		SkinnedMeshRenderer skinned = targetRenderer as SkinnedMeshRenderer;
		if (skinned == null)
			skinned = GetComponentInChildren<SkinnedMeshRenderer>();

		if (skinned == null || blendShapeIndex < 0)
			return;

		skinned.SetBlendShapeWeight(blendShapeIndex, weight);
	}
}
