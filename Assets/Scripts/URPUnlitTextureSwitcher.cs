
using UnityEngine;

[DisallowMultipleComponent]
public class URPUnlitTextureSwitcher : MonoBehaviour
{
	[Header("Material")]
	[SerializeField] private Renderer targetRenderer;
	[SerializeField] private int materialIndex;

	[Header("Frames")]
	[SerializeField] private Texture[] textures;

	[Header("Animação")]
	[Min(0.01f)]
	[SerializeField] private float framesPerSecond = 2f;

	[SerializeField] private bool playOnStart = true;

	[Header("URP Unlit")]
	[SerializeField] private string textureProperty = "_BaseMap";

	private MaterialPropertyBlock propertyBlock;
	private int currentFrame;
	private float timer;
	private bool isPlaying;

	private void Awake()
	{
		if (targetRenderer == null)
			targetRenderer = GetComponent<Renderer>();

		propertyBlock = new MaterialPropertyBlock();
	}

	private void Start()
	{
		if (textures == null || textures.Length == 0)
			return;

		ApplyFrame(0);

		isPlaying = playOnStart;
	}

	private void Update()
	{
		if (!isPlaying || textures == null || textures.Length <= 1)
			return;

		timer += Time.deltaTime;

		float frameDuration = 1f / framesPerSecond;

		if (timer >= frameDuration)
		{
			timer -= frameDuration;

			currentFrame = (currentFrame + 1) % textures.Length;

			ApplyFrame(currentFrame);
		}
	}

	private void ApplyFrame(int index)
	{
		if (targetRenderer == null ||
			textures == null ||
			index < 0 ||
			index >= textures.Length ||
			textures[index] == null)
			return;

		targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
		propertyBlock.SetTexture(textureProperty, textures[index]);
		targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
	}

	public void Play()
	{
		isPlaying = true;
	}

	public void Stop()
	{
		isPlaying = false;
	}

	public void SetFrame(int index)
	{
		if (textures == null || textures.Length == 0)
			return;

		currentFrame = ((index % textures.Length) + textures.Length)
					   % textures.Length;

		timer = 0f;
		ApplyFrame(currentFrame);
	}
}
