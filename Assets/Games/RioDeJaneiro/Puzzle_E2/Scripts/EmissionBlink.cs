
using UnityEngine;

public class EmissionBlink : MonoBehaviour
{
	[Header("Estado")]
	public bool win;
	public bool wait = true;
	public bool lose;

	[Header("Cores")]
	public Color waitColor = Color.blue;
	public Color winColor = Color.green;
	public Color loseColor = Color.red;

	[Header("Piscada")]
	[Min(0.1f)] public float blinkSpeed = 1f;
	[Min(0f)] public float emissionIntensity = 2f;

	public Renderer[] renderers;

	private MaterialPropertyBlock propertyBlock;
	private static readonly int EmissionColorID =
		Shader.PropertyToID("_EmissionColor");

	private void Awake()
	{
		propertyBlock = new MaterialPropertyBlock();

		if (renderers == null || renderers.Length == 0)
			renderers = GetComponentsInChildren<Renderer>(true);
	}

	private void Start()
	{
		SetWait();
	}

	private void Update()
	{
		Color targetColor;

		if (win)
			targetColor = winColor;
		else if (lose)
			targetColor = loseColor;
		else if (wait)
			targetColor = waitColor;
		else
		{
			ApplyEmission(Color.black);
			return;
		}

		float blink = (Mathf.Sin(
			Time.time * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

		ApplyEmission(Color.Lerp(
			Color.black,
			targetColor * emissionIntensity,
			blink));
	}

	public void SetWait()
	{
		wait = true;
		win = false;
		lose = false;
	}

	public void SetWin()
	{
		wait = false;
		win = true;
		lose = false;
	}

	public void SetLose()
	{
		wait = false;
		win = false;
		lose = true;
	}

	private void ApplyEmission(Color color)
	{
		if (renderers == null)
			return;

		foreach (Renderer rend in renderers)
		{
			if (rend == null)
				continue;

			rend.GetPropertyBlock(propertyBlock);
			propertyBlock.SetColor(EmissionColorID, color);
			rend.SetPropertyBlock(propertyBlock);
		}
	}
}
