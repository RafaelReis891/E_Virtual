using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Tour de banners para o admin (root): N = proximo banner, B = anterior (ordem natural pela chave).
// Teleporta o jogador para um ponto de frente para o banner (distancia pelo tamanho, 3..25 m, no chao, com visada livre)
// e vira o olhar para o centro dele; ai o admin clica no banner (RemoteBannerClickDriver) para enviar a imagem.
// Nao faz nada para nao-admin. Cria-se sozinho (RuntimeInitializeOnLoadMethod), sem Inspector, sem editar cena.
public sealed class RemoteBannerTour : MonoBehaviour
{
    const string Tag = "BannerTour";
    const float DistMin = 3f, DistMax = 25f;

    static RemoteBannerTour instancia;

    static readonly FieldInfo CampoPitch = typeof(StarterAssets.FirstPersonController)
        .GetField("_cinemachineTargetPitch", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo CampoVelVertical = typeof(StarterAssets.FirstPersonController)
        .GetField("_verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly List<RemoteBanner> lista = new List<RemoteBanner>();
    readonly HashSet<string> enviados = new HashSet<string>();
    int indice = -1;
    bool listaSuja = true;
    float proximaBusca;

    // jogador (cache)
    Transform jogador;
    CharacterController cc;
    StarterAssets.FirstPersonController fpc;
    bool avisouSemJogador;

    GUIStyle estilo;
    Texture2D fundo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar() { instancia = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        var go = new GameObject("RemoteBannerTour");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<RemoteBannerTour>();
    }

    void OnEnable()
    {
        EmuseuSettingsCatalog.Changed += AoMudarCatalogo;
        SceneManager.sceneLoaded += AoCarregarCena;
        SceneManager.sceneUnloaded += AoDescarregarCena;
    }

    void OnDisable()
    {
        EmuseuSettingsCatalog.Changed -= AoMudarCatalogo;
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneUnloaded -= AoDescarregarCena;
    }

    void OnDestroy()
    {
        if (fundo != null) Destroy(fundo);
        if (instancia == this) instancia = null;
    }

    // Changed(key != null) so dispara quando um upload do admin termina (SetImageUrl); key == null = catalogo recarregado.
    void AoMudarCatalogo(string key)
    {
        if (!string.IsNullOrEmpty(key)) enviados.Add(key);
    }

    void AoCarregarCena(Scene s, LoadSceneMode m) { listaSuja = true; jogador = null; avisouSemJogador = false; }
    void AoDescarregarCena(Scene s) { listaSuja = true; }

    // ---------- loop ----------

    void Update()
    {
        if (!EmuseuSettingsCatalog.IsAdmin) return;

        if (listaSuja || (lista.Count == 0 && Time.unscaledTime >= proximaBusca))
        {
            Reconstruir();
            proximaBusca = Time.unscaledTime + 2f;
        }
        if (lista.Count == 0) return;

        bool proximo = Tecla(true);
        bool anterior = !proximo && Tecla(false);
        if (!proximo && !anterior) return;

        Reconstruir(); // pega banners novos/removidos desde a ultima vez
        if (lista.Count == 0) return;
        int n = lista.Count;
        indice = indice < 0 ? (proximo ? 0 : n - 1) : ((indice + (proximo ? 1 : -1)) % n + n) % n;
        IrPara(indice);
    }

    void Reconstruir()
    {
        string atual = indice >= 0 && indice < lista.Count && lista[indice] != null ? lista[indice].Chave : null;
        lista.Clear();
        RemoteBanner[] todos = FindObjectsByType<RemoteBanner>(FindObjectsInactive.Include);
        foreach (RemoteBanner b in todos) if (b != null) lista.Add(b);
        lista.Sort((a, b) =>
        {
            int c = CompararNatural(a.Chave, b.Chave);
            return c != 0 ? c : string.CompareOrdinal(Caminho(a.transform), Caminho(b.transform));
        });
        indice = -1;
        if (atual != null)
            for (int i = 0; i < lista.Count; i++)
                if (lista[i].Chave == atual) { indice = i; break; }
        if (listaSuja && lista.Count > 0) EmuseuDebug.Info(Tag, "tour pronto: " + lista.Count + " banners (N proximo, B anterior)");
        listaSuja = false;
    }

    static bool Tecla(bool proxima)
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Keyboard k = Keyboard.current;
            if (k != null && (proxima ? k.nKey : k.bKey).wasPressedThisFrame) return true;
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetKeyDown(proxima ? KeyCode.N : KeyCode.B)) return true;
        }
        catch (Exception) { }
#endif
        return false;
    }

    // ---------- jogador ----------

    bool AcharJogador()
    {
        if (jogador != null && jogador.gameObject.activeInHierarchy) return true;
        jogador = null; cc = null; fpc = null;

        fpc = FindAnyObjectByType<StarterAssets.FirstPersonController>();
        if (fpc != null)
        {
            jogador = fpc.transform;
            cc = fpc.GetComponent<CharacterController>();
            return true;
        }
        CharacterController escolhido = null;
        foreach (CharacterController c in FindObjectsByType<CharacterController>())
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            if (c.CompareTag("Player")) { escolhido = c; break; }
            if (escolhido == null) escolhido = c;
        }
        if (escolhido == null) return false;
        cc = escolhido;
        jogador = escolhido.transform;
        return true;
    }

    // ---------- teleporte ----------

    sealed class Candidato
    {
        public Vector3 pos, normal;
        public float dist, score;
        public bool noChao, visada, livre, deFrente;
    }

    void IrPara(int idx)
    {
        RemoteBanner rb = lista[idx];
        string rotulo = (idx + 1) + "/" + lista.Count + " " + rb.Chave;
        if (!AcharJogador())
        {
            if (!avisouSemJogador) { avisouSemJogador = true; EmuseuDebug.Warn(Tag, "jogador nao encontrado (intro ainda tocando?) - nada feito"); }
            return;
        }
        avisouSemJogador = false;

        Bounds b;
        Vector3 eixoFino;
        Medir(rb, out b, out eixoFino);

        float eyeOff = 1.375f;
        Transform alvoPitch = null;
        if (fpc != null && fpc.CinemachineCameraTarget != null) alvoPitch = fpc.CinemachineCameraTarget.transform;
        else
        {
            Camera cam = Camera.main;
            if (cam != null && cam.transform.IsChildOf(jogador)) alvoPitch = cam.transform;
        }
        if (alvoPitch != null) eyeOff = alvoPitch.position.y - jogador.position.y;

        Camera c0 = Camera.main;
        float vfov = c0 != null ? Mathf.Clamp(c0.fieldOfView, 20f, 120f) : 60f;
        float aspect = c0 != null && c0.aspect > 0.1f ? c0.aspect : (Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f);

        bool ccEstava = cc != null && cc.enabled;
        if (ccEstava) cc.enabled = false; // como o Teleport.cs: sem colisao do proprio CC durante a consulta/movimento
        Candidato melhor = null;
        try
        {
            melhor = Planejar(rb, b, eixoFino, eyeOff, vfov, aspect);
            if (melhor != null)
            {
                Vector3 olho = melhor.pos + Vector3.up * eyeOff;
                Vector3 dir = b.center - olho;
                float horiz = new Vector2(dir.x, dir.z).magnitude;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(-dir.y, Mathf.Max(0.001f, horiz)) * Mathf.Rad2Deg; // positivo = olhar para baixo

                jogador.SetPositionAndRotation(melhor.pos, Quaternion.Euler(0f, yaw, 0f));
                bool pitchOk = AplicarPitch(alvoPitch, pitch);
                Physics.SyncTransforms();
                EmuseuDebug.Info(Tag, rotulo + ": pos=" + melhor.pos.ToString("0.0") + " dist=" + melhor.dist.ToString("0.0") + "m normal=" + melhor.normal.ToString("0.00") +
                                 " chao=" + melhor.noChao + " visada=" + melhor.visada + " livre=" + melhor.livre + " paraCentro=" + melhor.deFrente +
                                 " yaw=" + yaw.ToString("0") + " pitch=" + (pitchOk ? pitch.ToString("0") : "(nao aplicado)") + " score=" + melhor.score.ToString("0.0"));
                if (!melhor.visada) EmuseuDebug.Warn(Tag, rotulo + ": nenhum ponto com visada livre; usando o menos ruim");
            }
            else
            {
                EmuseuDebug.Warn(Tag, rotulo + ": nao achei ponto valido, jogador nao movido");
            }
        }
        catch (Exception ex)
        {
            EmuseuDebug.Error(Tag, rotulo + ": " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            if (ccEstava && cc != null) cc.enabled = true;
        }

        if (melhor != null)
        {
            if (fpc != null)
            {
                fpc.DeactivateAntiGravity();
                try { if (CampoVelVertical != null) CampoVelVertical.SetValue(fpc, 0f); } catch (Exception) { }
            }
        }
    }

    bool AplicarPitch(Transform alvo, float pitch)
    {
        if (alvo == null) return false;
        if (fpc != null)
        {
            pitch = Mathf.Clamp(pitch, fpc.BottomClamp, fpc.TopClamp);
            if (CampoPitch != null)
            {
                try { CampoPitch.SetValue(fpc, pitch); }
                catch (Exception ex) { EmuseuDebug.Warn(Tag, "pitch (reflexao) falhou: " + ex.Message); return false; }
            }
            else
            {
                EmuseuDebug.Warn(Tag, "campo _cinemachineTargetPitch nao encontrado: so a rotacao do alvo foi ajustada (o proximo movimento do mouse pode soltar o pitch)");
            }
        }
        alvo.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        return true;
    }

    Candidato Planejar(RemoteBanner rb, Bounds b, Vector3 eixoFino, float eyeOff, float vfov, float aspect)
    {
        Vector3 centroEstadio = CentroEstadio();
        Vector3 paraCentro = centroEstadio - b.center;
        paraCentro.y = 0f;
        paraCentro = paraCentro.sqrMagnitude > 0.01f ? paraCentro.normalized : Vector3.zero;

        // direcoes candidatas (horizontais): eixo fino do mesh (+-), forward do transform (+-), right (+-), direcao do centro.
        var normais = new List<Vector3>();
        var bonus = new List<float>();
        Action<Vector3, float> add = (v, bn) =>
        {
            v.y = 0f;
            if (v.sqrMagnitude < 0.25f) return; // quase vertical: nao serve como "frente" horizontal
            v.Normalize();
            for (int i = 0; i < normais.Count; i++) if (Vector3.Dot(normais[i], v) > 0.97f) return;
            normais.Add(v); bonus.Add(bn);
        };
        if (eixoFino != Vector3.zero) { add(eixoFino, 1f); add(-eixoFino, 1f); }
        add(rb.transform.forward, 0.5f); add(-rb.transform.forward, 0.5f);
        add(rb.transform.right, 0f); add(-rb.transform.right, 0f);
        if (paraCentro != Vector3.zero) add(paraCentro, 0f);
        if (normais.Count == 0) { add(Vector3.forward, 0f); add(Vector3.back, 0f); }

        float tanV = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * aspect;
        Vector3 e = b.extents;
        float[] fatores = { 1f, 0.75f, 0.55f };

        Candidato melhor = null;
        for (int ni = 0; ni < normais.Count; ni++)
        {
            Vector3 n = normais[ni];
            Vector3 perp = new Vector3(n.z, 0f, -n.x);
            float largura = 2f * (e.x * Mathf.Abs(perp.x) + e.z * Mathf.Abs(perp.z));
            float altura = 2f * e.y;
            float profundidade = 2f * (e.x * Mathf.Abs(n.x) + e.z * Mathf.Abs(n.z));
            float distBase = Mathf.Max(altura * 0.5f / tanV, Mathf.Max(0.1f, largura) * 0.5f / tanH) * 1.1f + profundidade * 0.5f;

            float ultima = -1f;
            for (int fi = 0; fi < fatores.Length; fi++)
            {
                float d = Mathf.Clamp(distBase * fatores[fi], DistMin, DistMax);
                if (Mathf.Abs(d - ultima) < 0.25f) continue;
                ultima = d;

                Candidato c = Avaliar(rb, b, n, d, eyeOff);
                c.score += bonus[ni] - 0.3f * fi;
                if (paraCentro != Vector3.zero && Vector3.Dot(n, paraCentro) > 0f) { c.deFrente = true; c.score += 1.5f; }
                if (melhor == null || c.score > melhor.score) melhor = c;
            }
        }
        return melhor;
    }

    Candidato Avaliar(RemoteBanner rb, Bounds b, Vector3 n, float dist, float eyeOff)
    {
        var c = new Candidato { normal = n, dist = dist };
        Vector3 xz = new Vector3(b.center.x, 0f, b.center.z) + n * dist;

        // chao: raio para baixo a partir da altura do banner (+1 m), ignora gatilhos, o proprio jogador e o banner
        float topo = b.center.y + 1f;
        float y = jogador.position.y;
        float pisoOffset = PesDoCC(); // distancia do pivo do jogador ate a base do CC (>= 0 se o pivo esta acima dos pes)
        bool achou = false;
        RaycastHit[] hs = Physics.RaycastAll(new Vector3(xz.x, topo, xz.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hs, (a, bb) => a.distance.CompareTo(bb.distance));
        foreach (RaycastHit h in hs)
        {
            if (Ignorar(h.collider, rb, false)) continue;
            if (h.normal.y < 0.5f) continue; // parede/teto, nao piso
            y = h.point.y + pisoOffset + 0.05f;
            achou = true;
            break;
        }
        c.noChao = achou;
        c.pos = new Vector3(xz.x, y, xz.z);

        // espaco livre para a capsula do jogador
        c.livre = CapsulaLivre(c.pos, rb);

        // visada do olho ate o centro do banner (tolera obstaculos nos ultimos 0.6 m: parede atras do banner)
        Vector3 olho = c.pos + Vector3.up * eyeOff;
        Vector3 ate = b.center - olho;
        float len = ate.magnitude;
        c.visada = true;
        if (len > 0.1f)
        {
            RaycastHit[] vs = Physics.RaycastAll(olho, ate / len, len, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in vs)
            {
                if (h.distance > len - 0.6f) continue;
                if (Ignorar(h.collider, rb, true)) continue;
                c.visada = false;
                break;
            }
        }

        c.score = (c.visada ? 4f : 0f) + (c.livre ? 2f : 0f) + (c.noChao ? 1f : 0f);
        return c;
    }

    float PesDoCC()
    {
        if (cc == null) return 0.07f; // PlayerCapsule: centro 0.93, altura 2 -> base a -0.07 do pivo
        Vector3 ls = jogador.lossyScale;
        float baseLocal = (cc.center.y - cc.height * 0.5f) * Mathf.Abs(ls.y);
        return -baseLocal;
    }

    bool CapsulaLivre(Vector3 pos, RemoteBanner rb)
    {
        float r = 0.45f, h = 2f;
        Vector3 centro = pos + Vector3.up * 0.93f;
        if (cc != null)
        {
            Vector3 ls = jogador.lossyScale;
            r = Mathf.Max(0.1f, cc.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)) - 0.05f);
            h = Mathf.Max(2f * r + 0.01f, cc.height * Mathf.Abs(ls.y) - 0.3f);
            centro = pos + new Vector3(cc.center.x * ls.x, cc.center.y * ls.y + 0.1f, cc.center.z * ls.z);
        }
        float meio = Mathf.Max(0f, h * 0.5f - r);
        Collider[] cs = Physics.OverlapCapsule(centro + Vector3.up * meio, centro - Vector3.up * meio, r, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider col in cs)
        {
            if (!Ignorar(col, rb, true)) return false;
        }
        return true;
    }

    // true = collider que nao conta como obstaculo/piso (jogador, o proprio banner, e - se visada - paredes fantasma).
    bool Ignorar(Collider col, RemoteBanner rb, bool testarFantasma)
    {
        if (col == null) return true;
        Transform t = col.transform;
        if (jogador != null && t.IsChildOf(jogador)) return true;
        if (t.IsChildOf(rb.transform)) return true;
        string nome = col.name;
        if (nome.StartsWith("Limite_", StringComparison.OrdinalIgnoreCase) || nome.StartsWith("Trigger_", StringComparison.OrdinalIgnoreCase)) return true;
        if (testarFantasma && !(col is TerrainCollider))
        {
            Renderer r = col.GetComponent<Renderer>();
            if (r == null) r = col.GetComponentInParent<Renderer>();
            if (r == null || !r.enabled) return true; // collider sem renderer ligado: o jogador ve atraves dele
        }
        return false;
    }

    // ---------- geometria do banner ----------

    static void Medir(RemoteBanner rb, out Bounds bounds, out Vector3 eixoFino)
    {
        eixoFino = Vector3.zero;
        bool tem = false;
        bounds = new Bounds(rb.transform.position, Vector3.one * 2f);
        var renderers = new List<Renderer>();
        Renderer own = rb.GetComponent<Renderer>();
        if (own != null) renderers.Add(own);
        else foreach (Renderer r in rb.GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) renderers.Add(r);

        foreach (Renderer r in renderers)
        {
            Bounds rbnd = r.bounds;
            if (rbnd.size.sqrMagnitude < 1e-6f) continue;
            if (!tem) { bounds = rbnd; tem = true; } else bounds.Encapsulate(rbnd);
        }
        if (!tem) return;

        // eixo fino: menor dimensao do mesh (em escala de mundo), se for claramente menor que as outras
        foreach (Renderer r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Vector3 s = mf.sharedMesh.bounds.size;
            Vector3 ls = r.transform.lossyScale;
            s = new Vector3(s.x * Mathf.Abs(ls.x), s.y * Mathf.Abs(ls.y), s.z * Mathf.Abs(ls.z));
            int menor = 0;
            if (s.y < s[menor]) menor = 1;
            if (s.z < s[menor]) menor = 2;
            float a = s[(menor + 1) % 3], c = s[(menor + 2) % 3];
            float segundo = Mathf.Min(a, c);
            if (s[menor] < 0.6f * segundo)
            {
                Vector3 eixo = Vector3.zero;
                eixo[menor] = 1f;
                eixoFino = r.transform.TransformDirection(eixo).normalized;
            }
            break;
        }
    }

    Vector3 CentroEstadio()
    {
        Vector3 soma = Vector3.zero;
        int n = 0;
        foreach (RemoteBanner b in lista)
        {
            if (b == null) continue;
            Vector3 p = b.transform.position;
            Bounds bb; Vector3 ef;
            Medir(b, out bb, out ef);
            p = bb.center;
            if (b.Chave == "logo_campo_centro") return p;
            soma += p; n++;
        }
        return n > 0 ? soma / n : Vector3.zero;
    }

    // ---------- GUI ----------

    void OnGUI()
    {
        if (!EmuseuSettingsCatalog.IsAdmin || lista.Count == 0) return;
        if (jogador == null || !jogador.gameObject.activeInHierarchy) return; // intro/cutscene: sem jogador ativo

        if (estilo == null)
        {
            fundo = new Texture2D(1, 1);
            fundo.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            fundo.Apply();
            estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false };
            estilo.normal.textColor = Color.white;
        }
        estilo.fontSize = Mathf.Max(13, Screen.height / 45);

        string txt;
        if (indice >= 0 && indice < lista.Count && lista[indice] != null)
        {
            string k = lista[indice].Chave;
            txt = "Banner " + (indice + 1) + "/" + lista.Count + ": " + k + (enviados.Contains(k) ? "  [enviado]" : "") +
                  "  (N proximo, B anterior, clique para enviar)";
        }
        else
        {
            txt = "Banner -/" + lista.Count + "  (N proximo, B anterior, clique para enviar)";
        }

        Vector2 tam = estilo.CalcSize(new GUIContent(txt));
        float w = tam.x + 30f, h = tam.y + 10f;
        var r = new Rect((Screen.width - w) * 0.5f, 10f, w, h);
        GUI.DrawTexture(r, fundo);
        GUI.Label(r, txt, estilo);
    }

    // ---------- utilidades ----------

    static string Caminho(Transform t)
    {
        string p = t.name;
        for (Transform q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
        return p;
    }

    // Ordem natural: banner_escadaria_2 antes de banner_escadaria_10.
    static int CompararNatural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                string na = a.Substring(si, i - si).TrimStart('0');
                string nb = b.Substring(sj, j - sj).TrimStart('0');
                if (na.Length != nb.Length) return na.Length < nb.Length ? -1 : 1;
                int c = string.CompareOrdinal(na, nb);
                if (c != 0) return c;
            }
            else
            {
                if (a[i] != b[j]) return a[i] < b[j] ? -1 : 1;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}