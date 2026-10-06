using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class OtfTests
    {
        internal static class CFF1Fonts
        {
            public static string Glametrix;

            public static string Quicksand_Regular;

            public static string SourceSans3_Regular;
        }
        
        internal static class CFF2Fonts
        {
            public static string AdobeVFPrototype;

            public static string SourceHanSerifVFProtoJP;
        }
        
        internal static class FontCollections
        {
            public static string Ttf_Asana;

            public static string Cff1_NotoSansCJK_Regular;
        }
        
        
        static OtfTests()
        {
            CFF1Fonts.Glametrix = Path.Combine("OTFFonts", "CFF", "Glametrix-oj9A.otf");
            CFF1Fonts.Quicksand_Regular = Path.Combine("OTFFonts", "CFF", "Quicksand-Regular.otf");
            CFF1Fonts.SourceSans3_Regular = Path.Combine("OTFFonts", "SourceSans3-Regular.otf");
            
            CFF2Fonts.AdobeVFPrototype = Path.Combine("OTFFonts", "CFF2", "AdobeVFPrototype.otf");
            CFF2Fonts.SourceHanSerifVFProtoJP = Path.Combine("OTFFonts", "CFF2", "SourceHanSerifVFProtoJP.otf");
            
            FontCollections.Ttf_Asana = Path.Combine("OTFFonts", "FontCollections", "ASANA.TTC");
            FontCollections.Cff1_NotoSansCJK_Regular = Path.Combine("OTFFonts", "FontCollections", "NotoSansCJK-Regular.ttc");
        }
        
        [Test]
        public void LoadOtfCff1Font_Glametrix()
        {
            var typeFace = Typeface.LoadFont(CFF1Fonts.Glametrix, 2);
        }
        
        [Test]
        public void LoadOtfCff1Font_SourceSans3()
        {
            var typeFace = Typeface.LoadFont(CFF1Fonts.SourceSans3_Regular, 2);
        }
        
        [Test]
        public void LoadOtfCff1Font_Quicksand_Regular()
        {
            var typeFace = Typeface.LoadFont(CFF1Fonts.Quicksand_Regular, 2);
        }
        
        [Test]
        public void LoadOtfCff2Font_AdobeVPPrototype()
        {
            var typeFace = Typeface.LoadFont(CFF2Fonts.AdobeVFPrototype, 2);
        }
        
        [Test]
        public void LoadOtfCff2Font_SourceHanSerifVFProtoJP()
        {
            var typeFace = Typeface.LoadFont(CFF2Fonts.SourceHanSerifVFProtoJP, 2);
        }
        
        [Test]
        public void LoadOtfCff1FontCollection_NotoSansCJK_Regular()
        {
            var typeFace = Typeface.LoadFont(FontCollections.Cff1_NotoSansCJK_Regular, 2);
        }
        
        [Test]
        public void LoadOtfCff1FontCollection_Asana()
        {
            var typeFace = Typeface.LoadFont(FontCollections.Ttf_Asana, 2);
        }
        
        [Test]
        public void OutputFeatures_SourceSans3_Regular()
        {
            var typeFace = Typeface.LoadFont(CFF1Fonts.SourceSans3_Regular, 2);
            foreach (var font in typeFace.Fonts)
            {
                foreach (var script in font.FeatureCatalog.Scripts)
                {
                    foreach (var language in script.Languages)
                    {
                        Debug.WriteLine($"Script: {script}, language: {language}");
                        foreach (var feature in language.Features)
                        {
                            Debug.WriteLine(feature);
                        }
                    }
                }
            }
        }

        [Test]
        public async Task FontManager_Test()
        {
            var fontManager = await FontService.LoadTypeFaceAsync(CFF1Fonts.SourceSans3_Regular);
            var font = fontManager.GetTypeFace(0).GetFont(0);

            var glyphs = Adamantium.Fonts.Shaping.TextShaper.Shape(font, "rw");

            Assert.That(glyphs, Has.Length.EqualTo(2));
        }
    }
}