using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vegetacao.EditorTools
{
    public enum VegOrigem
    {
        MalhaMesclada = 0, // extrai posicoes de uma malha mesclada (FBX/objeto da cena) por ilhas
        Csv = 1            // le posicoes de um CSV
    }

    public enum VegEscala
    {
        Nenhuma = 0,      // escala 1 x multiplicador
        Altura = 1,       // escala uniforme: altura da fonte / altura do prefab
        Copa = 2,         // escala uniforme: largura da copa da fonte / largura do prefab
        AlturaECopa = 3   // Y pela altura, XZ pela copa
    }

    [Serializable]
    public class VegClasse
    {
        public string nome = "Classe";
        public bool ativa = true;

        [Header("Origem das posicoes")]
        public VegOrigem origem = VegOrigem.MalhaMesclada;
        [Tooltip("FBX/prefab com a malha mesclada (usado se o objeto de cena abaixo nao for achado).")]
        public GameObject fbxFonte;
        [Tooltip("Nome do objeto na cena (ex.: Pampulha_VEG_PALMEIRAS). Aceita sufixo ' (1)'. Usado como fonte (transform do mundo) e DESATIVADO ao substituir.")]
        public string objetoCena;
        public TextAsset csv;
        [Tooltip("Coluna 'type' do CSV a filtrar (vazio = todas as linhas).")]
        public string csvTipo;
        [Tooltip("Na origem por malha mesclada, ignora ilhas com menos triangulos que este valor (ex.: folhas soltas). 0 = nao filtrar.")]
        public int extrMinTriangulos = 0;

        [Header("Substituicao")]
        public List<GameObject> prefabs = new List<GameObject>();
        public VegEscala escala = VegEscala.Altura;
        public float multiplicador = 1f;
        public float escalaMin = 0.3f;
        public float escalaMax = 3f;
        public bool rotacaoAleatoria = true;
        [Tooltip("Marca apenas BatchingStatic (nao mexe em GI/oclusao). Prefira desligado para prefabs com GPU Instancing.")]
        public bool batchingStatic = false;
    }

    [CreateAssetMenu(fileName = "PresetVegetacao", menuName = "Vegetacao/Preset de Vegetacao")]
    public class VegetacaoPreset : ScriptableObject
    {
        public string nomeRaiz = "Vegetacao_Individual";
        [Tooltip("Nome do objeto pai na cena (ex.: Vegetacao). Vazio ou nao achado = raiz da cena.")]
        public string paiNome = "Vegetacao";
        public int seed = 12345;
        public bool desativarOriginais = true;

        [Header("Extracao por ilhas (malha mesclada)")]
        public float extrRazaoFino = 0.5f;
        public float extrTolY = 1f;
        public float extrDistMax = 15f;

        public List<VegClasse> classes = new List<VegClasse>();
    }
}
