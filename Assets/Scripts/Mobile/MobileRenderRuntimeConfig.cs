// MobileRenderRuntimeConfig.cs
// Configuracao lida em tempo de execucao por MobileRenderRuntime.
// Este asset (Assets/Resources/MobileRenderRuntimeConfig.asset) so existe depois que
// voce clica em "Aplicar" em Mobile > Tools > Iluminacao e Render mobile...
// Sem o asset, MobileRenderRuntime nao faz nada.
using UnityEngine;

namespace EVirtualMobile
{
    public class MobileRenderRuntimeConfig : ScriptableObject
    {
        [Tooltip("Liga/desliga tudo sem precisar apagar o asset.")]
        public bool ativo = true;

        [Header("Quando aplicar")]
        [Tooltip("Nome do Quality Level considerado 'mobile'.")]
        public string nivelMobile = "Mobile";
        [Tooltip("Se marcado, so aplica quando Application.isMobilePlatform (celular/tablet no navegador WebGL). " +
                 "Se desmarcado, aplica sempre que o Quality Level ativo for o nivelMobile (tambem no PC). " +
                 "Na build unica PC+celular, deixe marcado.")]
        public bool somenteEmCelular = true;
        [Tooltip("Em celular, troca o Quality Level para o nivelMobile ao iniciar. O PC nao e afetado. " +
                 "Util se o Quality padrao do WebGL for o nivel de PC.")]
        public bool trocarNivelNoCelular = true;

        [Header("Pos-processamento (so na copia em runtime dos Volumes; os perfis originais nao mudam)")]
        public bool desligarDepthOfField = true;
        public bool desligarMotionBlur = true;
        public bool desligarChromaticAberration = true;
        public bool desligarFilmGrain = true;
        public bool desligarLensDistortion = true;
        public bool desligarPaniniProjection = true;
        public bool desligarVignette = false;
        [Tooltip("0 = manter, 1 = leve (sem high quality, 1/4 de resolucao, menos iteracoes), 2 = desligar")]
        public int bloom = 2;

        [Header("Cameras")]
        [Tooltip("-1 = manter, 0 = nenhum, 1 = FXAA")]
        public int antiAliasingCamera = 0;
        [Tooltip("Desliga o pos-processamento inteiro nas cameras (some tambem a correcao de cor).")]
        public bool desligarPosNaCamera = false;

        public bool logNoConsole = true;
    }
}
