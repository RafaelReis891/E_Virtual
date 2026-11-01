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
// - N/B pulam banners que ja tem imagem (catalogo do site tem url, ou enviado nesta sessao). Shift+N / Shift+B inclui todos.
// - So teleporta para ponto SEGURO: chao achado por raio para baixo (ignora gatilhos/jogador/banner), dentro dos limites
//   (o caminho reto de um ponto conhecido dentro do estadio ate o candidato nao atravessa nenhum collider 'Limite_*'),
//   capsula livre e visada livre. Sem ponto seguro: nao move, loga e mostra 'sem ponto seguro para <chave>'.
// Nao faz nada para nao-admin. Cria-se sozinho (RuntimeInitializeOnLoadMethod), sem Inspector, sem editar cena.
public sealed class RemoteBannerTour : MonoBehaviour
{
    const string Tag = "BannerTour";
    const float DistMin = 3f, DistMax = 25f;
    const float DistBusca = 60f;          // busca progressiva (sem ponto seguro na faixa normal): ate 60 m
    const float ChaoAbaixoMax = 45f;      // chao tem que estar a ate 45 m abaixo do topo (centro do banner + 1 m)
    const float AnguloVertMax = 65f;      // olhar do ponto ate o centro do banner: ate 65 graus para cima/baixo

    // Ja enviados pelo Teco antes desta sessao (o payload do site pode ainda nao refletir); alem disso vale o catalogo.
    static readonly string[] EnviadosConhecidos =
    {
        "banner_escadaria_1", "banner_escadaria_5", "banner_escadaria_6", "banner_vert_big_3",
        "led_hub", "logo_campo_1", "logo_campo_2", "logo_campo_centro",
    };

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

    // ultimo ponto seguro de tour (referencia "dentro dos limites") e mensagem curta na tela
    Vector3 ultimoSeguro;
    bool temUltimoSeguro;
    string mensagemGui;
    float mensagemAte;
    bool avisouSemLimites;

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
        foreach (string k in EnviadosConhecidos) enviados.Add(k);
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

    void AoCarregarCena(Scene s, LoadSceneMode m) { listaSuja = true; jogador = null; avisouSemJogador = false; temUltimoSeguro = false; avisouSemLimites = false; }
    void AoDescarregarCena(Scene s) { listaSuja = true; }

    // ---------- loop ----------

    void Update()
    {
        if (!EmuseuSettingsCatalog.IsAdmin) return;
        if (EmuseuUploadModalGuard.Ativo) return; // modal de upload aberto: o teclado e do React

        if (listaSuja || (lista.Count == 0 && Time.unscaledTime >= proximaBusca))
        {
            Reconstruir();
            proximaBusca = Time.unscaledTime + 2f;
        }
        if (lista.Count == 0) return;

        bool proximo = Tecla(true);
        bool anterior = !proximo && Tecla(false);
        if (!proximo && !anterior) return;

        bool todos = ShiftSegurado();
        Reconstruir(); // pega banners novos/removidos desde a ultima vez
        if (lista.Count == 0) return;
        int n = lista.Count;
        int passo = proximo ? 1 : -1;
        int inicio = indice < 0 ? (proximo ? -1 : n) : indice;
        int alvo = -1;
        for (int k = 1; k <= n; k++)
        {
            int cand = ((inicio + passo * k) % n + n) % n;
            if (todos || Pendente(lista[cand])) { alvo = cand; break; }
        }
        if (alvo < 0)
        {
            Mensagem("todos os banners ja tem imagem (Shift+N / Shift+B para revisar todos)", 4f);
            EmuseuDebug.Info(Tag, "nenhum banner pendente (todos com imagem); Shift+N/B inclui todos");
            return;
        }
        indice = alvo;
        IrPara(indice);
    }

    // Banner sem imagem no site (catalogo sem url) e nao enviado nesta sessao.
    bool Pendente(RemoteBanner b)
    {
        if (b == null) return false;
        string k = b.Chave;
        if (enviados.Contains(k)) return false;
        string url;
        return !EmuseuSettingsCatalog.TryGetImageUrl(k, out url);
    }

    int ContarPendentes()
    {
        int c = 0;
        foreach (RemoteBanner b in lista) if (Pendente(b)) c++;
        return c;
    }

    // posicao (1-based) do banner atual entre os pendentes
    int PosicaoPendente(int idx)
    {
        int c = 0;
        for (int i = 0; i <= idx && i < lista.Count; i++) if (Pendente(lista[i])) c++;
        return Mathf.Max(1, c);
    }

    void Mensagem(string txt, float segundos)
    {
        mensagemGui = txt;
        mensagemAte = Time.unscaledTime + segundos;
    }

    static bool ShiftSegurado()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Keyboard k = Keyboard.current;
            if (k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed)) return true;
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return true;
        }
        catch (Exception) { }
#endif
        return false;
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
        public bool noChao, borda, visada, livre, dentro, deFrente;
        public string etapa;
        public bool Seguro { get { return noChao && borda && livre && dentro; } }
    }

    void IrPara(int idx)
    {
        RemoteBanner rb = lista[idx];
        string rotulo = PosicaoPendente(idx) + "/" + ContarPendentes() + " pendentes " + rb.Chave;
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
                ultimoSeguro = melhor.pos;
                temUltimoSeguro = true;
                bool pitchOk = AplicarPitch(alvoPitch, pitch);
                Physics.SyncTransforms();
                EmuseuDebug.Info(Tag, rotulo + ": pos=" + melhor.pos.ToString("0.0") + " dist=" + melhor.dist.ToString("0.0") + "m normal=" + melhor.normal.ToString("0.00") +
                                 " etapa=" + melhor.etapa + " chao=" + melhor.noChao + " borda=" + melhor.borda + " dentroLimites=" + melhor.dentro +
                                 " visada=" + melhor.visada + " livre=" + melhor.livre + " paraCentro=" + melhor.deFrente +
                                 " yaw=" + yaw.ToString("0") + " pitch=" + (pitchOk ? pitch.ToString("0") : "(nao aplicado)") + " score=" + melhor.score.ToString("0.0"));
            }
            else
            {
                EmuseuDebug.Warn(Tag, rotulo + ": sem ponto seguro (chao + dentro dos Limite_* + capsula livre + visada); jogador NAO movido");
                Mensagem("sem ponto seguro para " + rb.Chave, 5f);
            }
        }
        catch (Exception ex)
        {
            EmuseuDebug.Error(Tag, rotulo + ": " + ex.GetType().Name + ": " + ex.Message);
            melhor = null; // erro no planejamento: nao teleporta
            Mensagem("sem ponto seguro para " + rb.Chave, 5f);
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

    // Planeja o ponto de visada. So devolve candidato SEGURO (chao + borda estavel + dentro dos limites + capsula livre)
    // E com visada livre; senao null (o chamador nao teleporta).
    //  etapa 1: frente do banner (eixo fino/forward/right/centro) a distancia pelo tamanho; todos os andares com chao.
    //  etapa 2: busca progressiva em anel ao redor do banner (12, 24 e 48 direcoes; 3..60 m).
    Candidato Planejar(RemoteBanner rb, Bounds b, Vector3 eixoFino, float eyeOff, float vfov, float aspect)
    {
        Vector3 centroEstadio = CentroEstadio();
        Vector3 paraCentro = centroEstadio - b.center;
        paraCentro.y = 0f;
        paraCentro = paraCentro.sqrMagnitude > 0.01f ? paraCentro.normalized : Vector3.zero;

        // referencias sabidamente dentro dos limites: centro do estadio e o ultimo ponto seguro do tour
        var refs = new List<Vector3>();
        refs.Add(centroEstadio);
        if (temUltimoSeguro) refs.Add(ultimoSeguro);

        var limites = new List<Collider>();
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            if (c != null && c.enabled && EhLimite(c)) limites.Add(c);
        if (limites.Count == 0 && !avisouSemLimites)
        {
            avisouSemLimites = true;
            EmuseuDebug.Warn(Tag, "nenhum collider 'Limite_*' ativo na cena: so a checagem de chao/capsula/visada vale");
        }

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

        var lote = new List<Candidato>();
        Candidato melhor = null;

        // ---- etapa 1
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

                lote.Clear();
                Avaliar(rb, b, n, d, eyeOff, refs, limites, "1", lote);
                foreach (Candidato c in lote)
                {
                    c.score += bonus[ni] - 0.3f * fi;
                    if (paraCentro != Vector3.zero && Vector3.Dot(n, paraCentro) > 0f) { c.deFrente = true; c.score += 1.5f; }
                    if (c.Seguro && c.visada && (melhor == null || c.score > melhor.score)) melhor = c;
                }
            }
        }
        if (melhor != null) return melhor;
        EmuseuDebug.Info(Tag, rb.Chave + ": etapa 1 sem ponto seguro com visada; buscando em anel (ate " + DistBusca.ToString("0") + " m)");

        // ---- etapa 2: mais direcoes, distancias crescentes (o primeiro anel com ponto seguro vence = o mais proximo)
        float[] distancias = { 3f, 5f, 8f, 12f, 16f, 20f, 25f, 30f, 40f, 50f, DistBusca };
        int[] etapas = { 12, 24, 48 };
        foreach (int dirs in etapas)
        {
            foreach (float d in distancias)
            {
                for (int di = 0; di < dirs; di++)
                {
                    float ang = di * 360f / dirs;
                    Vector3 n = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0f, Mathf.Cos(ang * Mathf.Deg2Rad));
                    lote.Clear();
                    Avaliar(rb, b, n, d, eyeOff, refs, limites, "2/" + dirs, lote);
                    foreach (Candidato c in lote)
                    {
                        if (!c.Seguro || !c.visada) continue;
                        if (paraCentro != Vector3.zero && Vector3.Dot(n, paraCentro) > 0f) { c.deFrente = true; c.score += 1.5f; }
                        if (eixoFino != Vector3.zero) c.score += Mathf.Abs(Vector3.Dot(n, eixoFino));
                        if (melhor == null || c.score > melhor.score) melhor = c;
                    }
                }
                if (melhor != null) return melhor;
            }
        }
        return null;
    }

    // Avalia todos os andares com chao sob o ponto (x,z) = centro do banner + n * dist; preenche 'saida'.
    void Avaliar(RemoteBanner rb, Bounds b, Vector3 n, float dist, float eyeOff, List<Vector3> refs, List<Collider> limites, string etapa, List<Candidato> saida)
    {
        Vector3 xz = new Vector3(b.center.x, 0f, b.center.z) + n * dist;

        // chao: raio para baixo a partir da altura do banner (+1 m), ate ChaoAbaixoMax; ignora gatilhos, o jogador, o banner e Limite_*.
        // Cada superficie de piso (normal para cima) distinta (>= 1 m) e um andar candidato (arquibancada, escadaria, campo...).
        float topo = b.center.y + 1f;
        float pisoOffset = PesDoCC(); // distancia do pivo do jogador ate a base do CC (>= 0 se o pivo esta acima dos pes)
        RaycastHit[] hs = Physics.RaycastAll(new Vector3(xz.x, topo, xz.z), Vector3.down, ChaoAbaixoMax + 1f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hs, (a, bb) => a.distance.CompareTo(bb.distance));

        float ultimoY = float.NaN;
        int andares = 0;
        foreach (RaycastHit h in hs)
        {
            if (andares >= 4) break;
            if (Ignorar(h.collider, rb, false)) continue;
            if (h.normal.y < 0.5f) continue; // parede/teto, nao piso
            if (!float.IsNaN(ultimoY) && Mathf.Abs(ultimoY - h.point.y) < 1f) continue;
            ultimoY = h.point.y;
            andares++;

            var c = new Candidato { normal = n, dist = dist, etapa = etapa, noChao = true };
            c.pos = new Vector3(xz.x, h.point.y + pisoOffset + 0.05f, xz.z);

            // angulo de visada ate o centro do banner (muito inclinado nao enquadra)
            Vector3 olho = c.pos + Vector3.up * eyeOff;
            float dh = new Vector2(b.center.x - olho.x, b.center.z - olho.z).magnitude;
            float ang = Mathf.Atan2(Mathf.Abs(b.center.y - olho.y), Mathf.Max(0.1f, dh)) * Mathf.Rad2Deg;
            if (ang > AnguloVertMax) continue;

            // 1) barato primeiro: borda estavel e capsula livre
            c.borda = ChaoEstavel(c.pos, pisoOffset, rb);
            c.livre = c.borda && CapsulaLivre(c.pos, rb);

            // 2) visada do olho ate o centro do banner (tolera obstaculos nos ultimos 0.6 m: parede atras do banner)
            c.visada = false;
            if (c.livre)
            {
                c.visada = true;
                Vector3 ate = b.center - olho;
                float len = ate.magnitude;
                if (len > 0.1f)
                {
                    RaycastHit[] vs = Physics.RaycastAll(olho, ate / len, len, ~0, QueryTriggerInteraction.Ignore);
                    foreach (RaycastHit v in vs)
                    {
                        if (v.distance > len - 0.6f) continue;
                        if (Ignorar(v.collider, rb, true)) continue;
                        c.visada = false;
                        break;
                    }
                }
            }

            // 3) por ultimo o mais caro: dentro dos limites (caminho reto das referencias seguras ate aqui sem cruzar Limite_*)
            c.dentro = true;
            if (c.visada && limites.Count > 0)
            {
                foreach (Vector3 r in refs)
                    if (CruzaLimite(r, c.pos, limites)) { c.dentro = false; break; }
            }

            c.score = (c.visada ? 4f : 0f) + (c.livre ? 2f : 0f) + (c.noChao ? 1f : 0f) + (c.dentro ? 2f : 0f)
                      - ang / 30f - dist * 0.02f;
            saida.Add(c);
        }
    }

    // Piso estavel: 4 raios para baixo em volta do ponto (0.35 m) tambem acham piso (normal para cima) perto da altura do chao.
    bool ChaoEstavel(Vector3 pos, float pisoOffset, RemoteBanner rb)
    {
        float chao = pos.y - pisoOffset - 0.05f;
        Vector3[] desl = { new Vector3(0.35f, 0f, 0f), new Vector3(-0.35f, 0f, 0f), new Vector3(0f, 0f, 0.35f), new Vector3(0f, 0f, -0.35f) };
        foreach (Vector3 d in desl)
        {
            Vector3 o = new Vector3(pos.x, chao + 0.6f, pos.z) + d;
            RaycastHit[] hs = Physics.RaycastAll(o, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hs, (a, bb) => a.distance.CompareTo(bb.distance));
            bool ok = false;
            foreach (RaycastHit h in hs)
            {
                if (Ignorar(h.collider, rb, false)) continue;
                ok = h.normal.y >= 0.5f && Mathf.Abs(h.point.y - chao) <= 0.9f;
                break;
            }
            if (!ok) return false;
        }
        return true;
    }

    // ---------- limites (Limite_*) ----------
    // Em CenaArena: Limite_Area_Player_Campo e Limite_Area_Player_HUB sao MeshCollider nao-convexos (mesh 'Limite_Area_Player'
    // do arena_vortice_v13.fbx): parede cilindrica aberta (sem tampa), MeshRenderer desligado. Nao e volume: nao da para testar
    // "ponto dentro". O teste certo e o do caminho: se o segmento referencia->candidato cruza a parede, o candidato esta do
    // outro lado (fora). Colliders de malha so acertam a face da frente (queriesHitBackfaces=false), entao ligamos
    // queriesHitBackfaces so durante o teste e lancamos nos dois sentidos.

    static bool EhLimite(Collider c)
    {
        for (Transform t = c.transform; t != null; t = t.parent)
            if (t.name.StartsWith("Limite_", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static bool TemLimite(RaycastHit[] hs)
    {
        foreach (RaycastHit h in hs)
            if (h.collider != null && EhLimite(h.collider)) return true;
        return false;
    }

    static bool SegmentoCruzaLimite(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return false;
        Vector3 dir = d / len;
        if (TemLimite(Physics.RaycastAll(a, dir, len, ~0, QueryTriggerInteraction.Collide))) return true;
        return TemLimite(Physics.RaycastAll(b, -dir, len, ~0, QueryTriggerInteraction.Collide));
    }

    // true = o caminho reto de 'refPos' ate 'pos' cruza algum Limite_*: 1 segmento 3D na altura do peito mais segmentos
    // horizontais nas alturas em que cada parede existe (entre as alturas do ponto de referencia e do candidato).
    static bool CruzaLimite(Vector3 refPos, Vector3 pos, List<Collider> limites)
    {
        bool anterior = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            Vector3 peito = Vector3.up * 1f;
            if (SegmentoCruzaLimite(refPos + peito, pos + peito)) return true;

            float yMin = Mathf.Min(refPos.y, pos.y);
            float yMax = Mathf.Max(refPos.y, pos.y) + 2.5f;
            foreach (Collider c in limites)
            {
                if (c == null) continue;
                Bounds cb = c.bounds;
                float lo = Mathf.Max(cb.min.y, yMin), hi = Mathf.Min(cb.max.y, yMax);
                if (lo > hi) continue; // parede nao existe nessa faixa de altura
                int amostras = hi - lo < 0.5f ? 1 : 3;
                for (int k = 0; k < amostras; k++)
                {
                    float h = amostras == 1 ? (lo + hi) * 0.5f : Mathf.Lerp(lo, hi, k / (amostras - 1f));
                    if (SegmentoCruzaLimite(new Vector3(refPos.x, h, refPos.z), new Vector3(pos.x, h, pos.z))) return true;
                }
            }
            return false;
        }
        finally { Physics.queriesHitBackfaces = anterior; }
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
        int pend = ContarPendentes();
        if (!string.IsNullOrEmpty(mensagemGui) && Time.unscaledTime < mensagemAte)
        {
            txt = mensagemGui;
        }
        else if (indice >= 0 && indice < lista.Count && lista[indice] != null)
        {
            string k = lista[indice].Chave;
            bool feito = !Pendente(lista[indice]);
            txt = "Banner " + PosicaoPendente(indice) + "/" + pend + " pendentes: " + k + (feito ? "  [enviado]" : "") +
                  "  (N proximo, B anterior, Shift+N/B todos, clique para enviar)";
        }
        else
        {
            txt = "Banner -/" + pend + " pendentes  (N proximo, B anterior, Shift+N/B todos, clique para enviar)";
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