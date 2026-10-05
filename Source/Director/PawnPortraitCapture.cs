using System;
using RimWorld;
using UnityEngine;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    /// <summary>
    /// The pawn's in-game portrait as a picture for a persona request: what it
    /// looks like - hair, face, scars, clothes - is something the text data only
    /// lists. Sent with requests about one pawn; a batch describes several and
    /// has no one picture. A model that takes no pictures refuses the request,
    /// and Communication then asks again without it.
    /// </summary>
    internal static class PawnPortraitCapture
    {
        const int Size = 512;
        const int JpegQuality = 88;
        const string PromptNote = "The attached image is this character's in-game RimWorld portrait.";

        /// <summary>
        /// Attaches the portrait when the context asks for one. Reads a render
        /// texture, so it does nothing off the main thread.
        /// </summary>
        public static void Attach(TalkRequest request, Pawn pawn, ContextSettings context)
        {
            if (request == null || context == null || !context.Inc_PawnPortrait) return;
            string jpeg = TryCaptureJpegBase64(pawn);
            if (jpeg == null) return;
            request.ImageJpegBase64 = jpeg;
            request.Prompt = (request.Prompt ?? string.Empty) + "\n\n" + PromptNote;
        }

        static string TryCaptureJpegBase64(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || !UnityData.IsInMainThread) return null;

            Texture2D copy = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture portrait = PortraitsCache.Get(
                    pawn, new Vector2(Size, Size), Rot4.South,
                    supersample: false, compensateForUIScale: false);
                if (portrait == null) return null;

                RenderTexture.active = portrait;
                copy = new Texture2D(portrait.width, portrait.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0f, 0f, portrait.width, portrait.height), 0, 0);
                copy.Apply(false, false);
                byte[] jpeg = copy.EncodeToJPG(JpegQuality);
                return jpeg == null || jpeg.Length == 0 ? null : Convert.ToBase64String(jpeg);
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — a Unity render/readback failure must cost the picture, not the request
            catch (Exception ex)
            {
                RimAiLog.WarningOnce(RimAiLogCategory.Personas, "[Director] Pawn portrait capture failed: " + ex, 0x50525452);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }
    }
}
