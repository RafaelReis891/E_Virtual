using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

// Versao simples da bussola: uma unica pergunta.
// Nao depende do GameManagerE2, NeedleController nem DirectionButton.
// Clique numa direcao -> a agulha gira ate ela.
//  Certo: pisca verde -> OnEndGame -> OnPassNextScene.
//  Errado: pisca vermelho, a agulha balanca, da uma volta e volta ao inicio.
public class BussolaSimples : MonoBehaviour
{
	[System.Serializable]
	public class Direcao
	{
		public string nome = "Norte";
		[Tooltip("Angulo Z local da agulha apontando para esta direcao.")]
		public float angulo;
		[Tooltip("Objeto do polo (N, L, S, O) com collider.")]
		public GameObject botao;
		[Tooltip("Luz desta direcao (opcional).")]
		public EmissionBlink luz;
	}

	[Header("Resposta")]
	[Tooltip("Nome da direcao correta (igual ao 'nome' de uma das direcoes).")]
	public string direcaoCorreta = "Norte";

	[Header("Direcoes")]
	public Direcao[] direcoes =
	{
		new Direcao { nome = "Norte", angulo = 0 },
		new Direcao { nome = "Leste", angulo = 90 },
		new Direcao { nome = "Sul", angulo = 180 },
		new Direcao { nome = "Oeste", angulo = 270 },
	};

	[Header("Agulha")]
	public Transform agulha;
	public float anguloInicial = 45f;
	public float velocidadeGiro = 180f;

	[Header("Animacao de erro")]
	public float amplitudeBalanco = 25f;
	public int balancos = 4;
	public float duracaoBalanco = 0.12f;
	public int voltas = 1;
	public float velocidadeVolta = 720f;
	public float tempoVermelho = 0.5f;

	[Header("Clique")]
	public Camera cameraJogador;
	public float alcanceClique = 100f;

	[Header("Texto (opcional)")]
	public TMP_Text textoFeedback;
	[TextArea(2, 4)] public string pergunta;
	public string textoAcerto = "Resposta correta!";
	public string textoErro = "Nao e essa direcao, tente novamente!";

	[Header("Fim")]
	public float tempoAteFim = 3f;
	public float tempoAteProximaCena = 3f;
	public UnityEvent OnEndGame, OnPassNextScene;

	bool ocupado, terminou;

	void Start()
	{
		foreach (Direcao d in direcoes)
			if (d.luz == null && d.botao != null) d.luz = d.botao.GetComponent<EmissionBlink>();
		if (cameraJogador == null) cameraJogador = Camera.main;
		if (agulha != null) agulha.localRotation = Quaternion.Euler(0, 0, anguloInicial);
		TodasAzul();
		MostrarTexto(pergunta);
	}

	void Update()
	{
		if (ocupado || terminou || cameraJogador == null || !Input.GetMouseButtonDown(0))
			return;

		Ray ray = cameraJogador.ScreenPointToRay(Input.mousePosition);
		RaycastHit[] hits = Physics.RaycastAll(ray, alcanceClique, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

		Direcao escolhida = null;
		float menor = float.MaxValue;
		foreach (RaycastHit h in hits)
			foreach (Direcao d in direcoes)
				if (d.botao != null && h.collider.transform.IsChildOf(d.botao.transform) && h.distance < menor)
				{ menor = h.distance; escolhida = d; }

		if (escolhida != null)
			StartCoroutine(Responder(escolhida));
	}

	IEnumerator Responder(Direcao d)
	{
		ocupado = true;
		TodasAzul();

		yield return GirarAte(d.angulo);

		bool certo = Igual(d.nome, direcaoCorreta);
		if (certo)
		{
			terminou = true;
			if (d.luz != null) d.luz.SetWin();
			MostrarTexto($"{pergunta}\n\n<color=green>{textoAcerto}</color>");
			yield return new WaitForSeconds(tempoAteFim);
			OnEndGame?.Invoke();
			yield return new WaitForSeconds(tempoAteProximaCena);
			OnPassNextScene?.Invoke();
			yield break;
		}

		if (d.luz != null) d.luz.SetLose();
		MostrarTexto($"{pergunta}\n\n<color=red>{textoErro}</color>");
		yield return AnimacaoErro();
		yield return new WaitForSeconds(tempoVermelho);

		TodasAzul();
		MostrarTexto(pergunta);
		ocupado = false;
	}

	IEnumerator GirarAte(float alvo)
	{
		if (agulha == null) yield break;
		while (Mathf.Abs(Mathf.DeltaAngle(agulha.localEulerAngles.z, alvo)) > 0.5f)
		{
			float z = Mathf.MoveTowardsAngle(agulha.localEulerAngles.z, alvo, velocidadeGiro * Time.deltaTime);
			agulha.localRotation = Quaternion.Euler(0, 0, z);
			yield return null;
		}
		agulha.localRotation = Quaternion.Euler(0, 0, alvo);
	}

	IEnumerator AnimacaoErro()
	{
		if (agulha == null) yield break;
		float baseZ = agulha.localEulerAngles.z;

		for (int i = 0; i < balancos; i++)
		{
			float amp = amplitudeBalanco * (1f - (float)i / balancos);
			yield return Interpolar(agulha.localEulerAngles.z, baseZ + (i % 2 == 0 ? amp : -amp), duracaoBalanco);
		}
		yield return Interpolar(agulha.localEulerAngles.z, baseZ, duracaoBalanco);

		float total = 360f * voltas + Mathf.Repeat(anguloInicial - baseZ, 360f), girado = 0f;
		while (girado < total)
		{
			girado += Mathf.Min(velocidadeVolta * Time.deltaTime, total - girado);
			agulha.localRotation = Quaternion.Euler(0, 0, baseZ + girado);
			yield return null;
		}
		agulha.localRotation = Quaternion.Euler(0, 0, anguloInicial);
	}

	IEnumerator Interpolar(float de, float para, float duracao)
	{
		for (float t = 0; t < duracao; t += Time.deltaTime)
		{
			agulha.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(de, para, Mathf.SmoothStep(0, 1, t / duracao)));
			yield return null;
		}
		agulha.localRotation = Quaternion.Euler(0, 0, para);
	}

	void TodasAzul()
	{
		foreach (Direcao d in direcoes)
			if (d.luz != null) d.luz.SetWait();
	}

	void MostrarTexto(string t)
	{
		if (textoFeedback != null && !string.IsNullOrEmpty(pergunta))
			textoFeedback.text = t;
	}

	static bool Igual(string a, string b) =>
		string.Equals(a?.Trim(), b?.Trim(), System.StringComparison.OrdinalIgnoreCase);
}