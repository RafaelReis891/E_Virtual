// MobileRenderRuntime.cs
// Ajustes de render em tempo de execucao para o caminho mobile (WebGL no navegador do celular).
// - So age se existir Resources/MobileRenderRuntimeConfig.asset (criado pela ferramenta
//   Mobile > Tools > Iluminacao e Render mobile... ao clicar em Aplicar).
// - Nao altera cenas, prefabs nem perfis de Volume no disco: usa volume.profile (copia em memoria).
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EVirtualMobile
{
    public static class MobileRenderRuntime
    {
        public const string NomeConfig = "MobileRenderRuntimeConfig";
        static MobileRenderRuntimeConfig cfg;
        static bool registrado;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Iniciar()
        {
            cfg = Resources.Load<MobileRenderRuntimeConfig>(NomeConfig);
            if (cfg == null || !cfg.ativo) return;

            if (cfg.trocarNivelNoCelular && Application.isMobilePlatform)
            {
                int idx = Array.IndexOf(QualitySettings.names, cfg.nivelMobile);
                if (idx >= 0 && QualitySettings.GetQualityLevel() != idx)
                {
                    QualitySettings.SetQualityLevel(idx, true);
                    if (cfg.logNoConsole) Debug.Log("[MobileRender] Quality Level trocado para " + cfg.nivelMobile);
                }
            }

            if (!registrado)
            {
                SceneManager.sceneLoaded += AoCarregarCena;
                registrado = true;
            }
        }

        static void AoCarregarCena(Scene cena, LoadSceneMode modo)
        {
            if (cfg == null || !cfg.ativo || !DeveAplicar()) return;
            AplicarAgora();
        }

        public static bool DeveAplicar()
        {
            if (cfg == null || !cfg.ativo) return false;
            if (cfg.somenteEmCelular) return Application.isMobilePlatform;
            int q = QualitySettings.GetQualityLevel();
            var nomes = QualitySettings.names;
            return q >= 0 && q < nomes.Length && nomes[q] == cfg.nivelMobile;
        }

        /// <summary>Pode ser chamado por outros scripts (ex.: depois de instanciar uma camera nova).</summary>
        public static void AplicarAgora()
        {
            if (cfg == null) return;
            int vols = 0, cams = 0;

            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
            foreach (var v in volumes)
            {
                if (v == null || !v.HasInstantiatedProfile() && v.sharedProfile == null) continue;
                var p = v.profile; // copia em memoria; o asset no disco nao muda
                if (p == null) continue;
                Desligar<DepthOfField>(p, cfg.desligarDepthOfField);
                Desligar<MotionBlur>(p, cfg.desligarMotionBlur);
                Desligar<ChromaticAberration>(p, cfg.desligarChromaticAberration);
                Desligar<FilmGrain>(p, cfg.desligarFilmGrain);
                Desligar<LensDistortion>(p, cfg.desligarLensDistortion);
                Desligar<PaniniProjection>(p, cfg.desligarPaniniProjection);
                Desligar<Vignette>(p, cfg.desligarVignette);

                Bloom b;
                if (p.TryGet(out b))
                {
                    if (cfg.bloom == 2) b.active = false;
                    else if (cfg.bloom == 1)
                    {
                        b.highQualityFiltering.Override(false);
                        b.downscale.Override(BloomDownscaleMode.Quarter);
                        b.maxIterations.Override(Mathf.Min(b.maxIterations.value, 4));
                    }
                }
                vols++;
            }

            var dados = UnityEngine.Object.FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsInactive.Include);
            foreach (var d in dados)
            {
                if (d == null) continue;
                if (cfg.antiAliasingCamera == 0) d.antialiasing = AntialiasingMode.None;
                else if (cfg.antiAliasingCamera == 1) d.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                if (cfg.desligarPosNaCamera) d.renderPostProcessing = false;
                cams++;
            }

            if (cfg.logNoConsole)
                Debug.Log("[MobileRender] Ajustes mobile aplicados: " + vols + " volume(s), " + cams + " camera(s).");
        }

        static void Desligar<T>(VolumeProfile p, bool desligar) where T : VolumeComponent
        {
            if (!desligar) return;
            T c;
            if (p.TryGet(out c)) c.active = false;
        }
    }
}
