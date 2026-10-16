
using UnityEngine;

[DisallowMultipleComponent]
public class MousePanelMovement : MonoBehaviour
{
	[Header("Referências")]
	[SerializeField] private RectTransform panel;
	[SerializeField] private RectTransform area;
	[SerializeField] private Canvas canvas;

	[Header("Movimento")]
	[SerializeField] private bool moverX = true;
	[SerializeField] private bool moverY = true;

	[Tooltip("Velocidade de resposta do Smooth.")]
	[Min(0f)]
	[SerializeField] private float suavidade = 12f;

	[Tooltip("Sensibilidade do movimento do mouse.")]
	[SerializeField] private float sensibilidade = 10f;

	private Vector2 posicaoAtual;
	private Vector2 posicaoAlvo;

	private void Awake()
	{
		if (panel == null)
			panel = GetComponent<RectTransform>();

		if (canvas == null)
			canvas = GetComponentInParent<Canvas>();

		if (panel == null || area == null || canvas == null)
		{
			Debug.LogError(
				"[MousePanelMovement] Configure Panel, Area e Canvas.",
				this
			);

			enabled = false;
			return;
		}

		// Converte a posição inicial do painel para o espaço local da área.
		Vector3 local = area.InverseTransformPoint(panel.position);

		posicaoAtual = new Vector2(local.x, local.y);
		posicaoAlvo = posicaoAtual;
	}

	private void Update()
	{
		// Obtém o movimento relativo do mouse, inclusive com cursor bloqueado.
		float mouseX = Input.GetAxisRaw("Mouse X");
		float mouseY = Input.GetAxisRaw("Mouse Y");

		if (moverX)
			posicaoAlvo.x += mouseX * sensibilidade;

		if (moverY)
			posicaoAlvo.y += mouseY * sensibilidade;

		// Restringe o movimento aos limites da área.
		Rect bounds = area.rect;

		Vector3 panelScale = panel.lossyScale;
		Vector3 areaScale = area.lossyScale;

		float width = panel.rect.width *
			Mathf.Abs(panelScale.x / areaScale.x);

		float height = panel.rect.height *
			Mathf.Abs(panelScale.y / areaScale.y);

		float minX = bounds.xMin + width * panel.pivot.x;
		float maxX = bounds.xMax - width * (1f - panel.pivot.x);

		float minY = bounds.yMin + height * panel.pivot.y;
		float maxY = bounds.yMax - height * (1f - panel.pivot.y);

		posicaoAlvo.x = Mathf.Clamp(posicaoAlvo.x, minX, maxX);
		posicaoAlvo.y = Mathf.Clamp(posicaoAlvo.y, minY, maxY);

		// Smooth independente da taxa de quadros.
		float t = suavidade <= 0f
			? 1f
			: 1f - Mathf.Exp(-suavidade * Time.unscaledDeltaTime);

		posicaoAtual = Vector2.Lerp(posicaoAtual, posicaoAlvo, t);

		Vector3 worldPosition = area.TransformPoint(
			new Vector3(posicaoAtual.x, posicaoAtual.y, 0f)
		);

		panel.position = worldPosition;
	}
}
