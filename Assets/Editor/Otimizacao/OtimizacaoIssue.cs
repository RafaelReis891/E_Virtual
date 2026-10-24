using System;
using UnityEditor;
using UnityEngine;

namespace EVirtual.EditorTools.Otimizacao
{
    public enum Severidade { Alto = 0, Medio = 1, Baixo = 2 }

    public enum Categoria { Texturas = 0, Malhas, Static, Luzes, Materiais, Audio, Fisica, UI, Limpeza }

    public class OtimizacaoIssue
    {
        public Categoria categoria;
        public Severidade severidade;
        public string nome;
        public string problema;
        public string sugestao;
        public UnityEngine.Object alvo;
        public bool marcado;
        public string chave;

        // Fix applied to scene objects or assets (not importers)
        public Action fixCena;
        // Fix applied to an AssetImporter (batched + SaveAndReimport)
        public string assetPath;
        public Action<AssetImporter> fixImporter;
        // If false, the issue is not included in "Corrigir todos"
        public bool incluirEmTodos = true;

        public bool PodeCorrigir { get { return fixCena != null || fixImporter != null; } }
    }

    public class OtimizacaoStats
    {
        public long triangulosCena;
        public int materiaisUnicos;
        public int luzesRealtime;
        public int texturasAnalisadas;
        public int malhasAnalisadas;
        public int audiosAnalisados;
        public int renderers;
    }

    public class OtimizacaoConfig
    {
        const string P = "EVirtual.Otimizacao.";
        public int maxTextura = 2048;
        public int webglFormato = 0; // 0 ASTC_6x6, 1 DXT1/DXT5, 2 DXT Crunched
        public int trisLod = 5000;
        public float cullPercent = 2f;
        public int trisMeshCollider = 2000;
        public float tamanhoSombraMin = 0.5f;
        public int maxLuzesRealtime = 4;
        public int instancingMinUsos = 10;
        public float audioLongo = 10f;
        public float audioCurto = 2f;
        public float audioSfxMono = 5f;
        public int canvasMaxGraphics = 100;
        public bool salvarRelatorio = true;

        public void Carregar()
        {
            maxTextura = EditorPrefs.GetInt(P + "maxTextura", maxTextura);
            webglFormato = EditorPrefs.GetInt(P + "webglFormato", webglFormato);
            trisLod = EditorPrefs.GetInt(P + "trisLod", trisLod);
            cullPercent = EditorPrefs.GetFloat(P + "cullPercent", cullPercent);
            trisMeshCollider = EditorPrefs.GetInt(P + "trisMeshCollider", trisMeshCollider);
            tamanhoSombraMin = EditorPrefs.GetFloat(P + "tamanhoSombraMin", tamanhoSombraMin);
            maxLuzesRealtime = EditorPrefs.GetInt(P + "maxLuzesRealtime", maxLuzesRealtime);
            instancingMinUsos = EditorPrefs.GetInt(P + "instancingMinUsos", instancingMinUsos);
            audioLongo = EditorPrefs.GetFloat(P + "audioLongo", audioLongo);
            audioCurto = EditorPrefs.GetFloat(P + "audioCurto", audioCurto);
            audioSfxMono = EditorPrefs.GetFloat(P + "audioSfxMono", audioSfxMono);
            canvasMaxGraphics = EditorPrefs.GetInt(P + "canvasMaxGraphics", canvasMaxGraphics);
            salvarRelatorio = EditorPrefs.GetBool(P + "salvarRelatorio", salvarRelatorio);
        }

        public void Salvar()
        {
            EditorPrefs.SetInt(P + "maxTextura", maxTextura);
            EditorPrefs.SetInt(P + "webglFormato", webglFormato);
            EditorPrefs.SetInt(P + "trisLod", trisLod);
            EditorPrefs.SetFloat(P + "cullPercent", cullPercent);
            EditorPrefs.SetInt(P + "trisMeshCollider", trisMeshCollider);
            EditorPrefs.SetFloat(P + "tamanhoSombraMin", tamanhoSombraMin);
            EditorPrefs.SetInt(P + "maxLuzesRealtime", maxLuzesRealtime);
            EditorPrefs.SetInt(P + "instancingMinUsos", instancingMinUsos);
            EditorPrefs.SetFloat(P + "audioLongo", audioLongo);
            EditorPrefs.SetFloat(P + "audioCurto", audioCurto);
            EditorPrefs.SetFloat(P + "audioSfxMono", audioSfxMono);
            EditorPrefs.SetInt(P + "canvasMaxGraphics", canvasMaxGraphics);
            EditorPrefs.SetBool(P + "salvarRelatorio", salvarRelatorio);
        }

        public void Padrao()
        {
            var c = new OtimizacaoConfig();
            maxTextura = c.maxTextura; webglFormato = c.webglFormato; trisLod = c.trisLod;
            cullPercent = c.cullPercent; trisMeshCollider = c.trisMeshCollider;
            tamanhoSombraMin = c.tamanhoSombraMin; maxLuzesRealtime = c.maxLuzesRealtime;
            instancingMinUsos = c.instancingMinUsos; audioLongo = c.audioLongo;
            audioCurto = c.audioCurto; audioSfxMono = c.audioSfxMono;
            canvasMaxGraphics = c.canvasMaxGraphics; salvarRelatorio = c.salvarRelatorio;
        }
    }
}
