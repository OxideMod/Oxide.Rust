extern alias Oxide;

using Oxide::Newtonsoft.Json;
using Oxide::Newtonsoft.Json.Converters;
using Oxide::Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Game.Rust.Cui
{
    public static class CuiHelper
    {
        public static string ToJson(List<CuiElement> elements, bool format = false)
        {
            return JsonConvert.SerializeObject(elements,
                format ? Formatting.Indented : Formatting.None,
                new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore
                }).Replace("\\n", "\n");
        }

        public static List<CuiElement> FromJson(string json) => JsonConvert.DeserializeObject<List<CuiElement>>(json);

        public static string GetGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);

        public static bool AddUi(BasePlayer player, List<CuiElement> elements) => AddUi(player, ToJson(elements));

        public static bool AddUi(BasePlayer player, string json)
        {
            if (player?.net == null) return false;

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", json);
            return true;
        }

        public static bool DestroyUi(BasePlayer player, string elem)
        {
            if (player?.net == null) return false;

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", elem);
            return true;
        }

        public static void SetColor(this ICuiColor elem, Color color)
        {
            elem.Color = $"{color.r} {color.g} {color.b} {color.a}";
        }

        public static Color GetColor(this ICuiColor elem) => ColorEx.Parse(elem.Color);
    }

    public class CuiElementContainer : List<CuiElement>
    {
        public string Add(CuiButton button, string parent = "Hud", string name = null)
        {
            if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();
            Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = button.FadeOut,
                Components =
                {
                    button.Button,
                    button.RectTransform
                }
            });
            if (!string.IsNullOrEmpty(button.Text.Text))
            {
                Add(new CuiElement
                {
                    Parent = name,
                    FadeOut = button.FadeOut,
                    Components =
                    {
                        button.Text,
                        new CuiRectTransformComponent()
                    }
                });
            }
            return name;
        }

        public string Add(CuiLabel label, string parent = "Hud", string name = null)
        {
            if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();
            Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = label.FadeOut,
                Components =
                {
                    label.Text,
                    label.RectTransform
                }
            });
            return name;
        }

        public string Add(CuiInputField inputField, string parent = "Hud", string name = null)
        {
            if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();
            Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = inputField.FadeOut,
                Components =
                {
                    inputField.InputField,
                    inputField.RectTransform
                }
            });
            return name;
        }

        public string Add(CuiPanel panel, string parent = "Hud", string name = null)
        {
            if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();
            var element = new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = panel.FadeOut
            };
            if (panel.Image != null) element.Components.Add(panel.Image);
            if (panel.RawImage != null) element.Components.Add(panel.RawImage);
            element.Components.Add(panel.RectTransform);
            if (panel.CursorEnabled) element.Components.Add(new CuiNeedsCursorComponent());
            Add(element);
            return name;
        }

        public string ToJson() => ToString();

        public override string ToString() => CuiHelper.ToJson(this);
    }

    public class CuiButton
    {
        public CuiButtonComponent Button { get; } = new CuiButtonComponent();
        public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
        public CuiTextComponent Text { get; } = new CuiTextComponent();
        public float FadeOut { get; set; }
    }

    public class CuiPanel
    {
        public CuiImageComponent Image { get; set; } = new CuiImageComponent();
        public CuiRawImageComponent RawImage { get; set; }
        public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
        public bool CursorEnabled { get; set; }
        public float FadeOut { get; set; }
    }

    public class CuiLabel
    {
        public CuiTextComponent Text { get; } = new CuiTextComponent();
        public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
        public float FadeOut { get; set; }
    }

    public class CuiInputField
    {
        public CuiInputFieldComponent InputField { get; } = new CuiInputFieldComponent();
        public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
        public float FadeOut { get; set; }
    }

    public class CuiElement
    {
        [DefaultValue("AddUI CreatedPanel")]
        [JsonProperty("name")]
        public string Name { get; set; } = "AddUI CreatedPanel";

        [JsonProperty("parent")]
        public string Parent { get; set; } = "Hud";

        [JsonProperty("components")]
        public List<ICuiComponent> Components { get; } = new List<ICuiComponent>();

        [JsonProperty("fadeOut")]
        public float FadeOut { get; set; }
    }

    [JsonConverter(typeof(ComponentConverter))]
    public interface ICuiComponent
    {
        [JsonProperty("type")]
        string Type { get; }
    }

    public interface ICuiColor
    {
        [DefaultValue("1.0 1.0 1.0 1.0")]
        [JsonProperty("color")]
        string Color { get; set; }
    }

    public class CuiTextComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Text";

        //The string value this text will display.
        [DefaultValue("Text")]
        [JsonProperty("text")]
        public string Text { get; set; } = "Text";

        //The size that the Font should render at.
        [DefaultValue(14)]
        [JsonProperty("fontSize")]
        public int FontSize { get; set; } = 14;

        //The Font used by the text.
        [DefaultValue("RobotoCondensed-Bold.ttf")]
        [JsonProperty("font")]
        public string Font { get; set; } = "RobotoCondensed-Bold.ttf";

        //The positioning of the text reliative to its RectTransform.
        [DefaultValue(TextAnchor.UpperLeft)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("align")]
        public TextAnchor Align { get; set; } = TextAnchor.UpperLeft;

        public string Color { get; set; } = "1.0 1.0 1.0 1.0";

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiImageComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Image";

        [DefaultValue("Assets/Content/UI/UI.Background.Tile.psd")]
        [JsonProperty("sprite")]
        public string Sprite { get; set; } = "Assets/Content/UI/UI.Background.Tile.psd";

        [DefaultValue("Assets/Icons/IconMaterial.mat")]
        [JsonProperty("material")]
        public string Material { get; set; } = "Assets/Icons/IconMaterial.mat";

        public string Color { get; set; } = "1.0 1.0 1.0 1.0";

        [DefaultValue(Image.Type.Simple)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("imagetype")]
        public Image.Type ImageType { get; set; } = Image.Type.Simple;

        [JsonProperty("png")]
        public string Png { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiRawImageComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.RawImage";

        [DefaultValue("Assets/Icons/rust.png")]
        [JsonProperty("sprite")]
        public string Sprite { get; set; } = "Assets/Icons/rust.png";

        public string Color { get; set; } = "1.0 1.0 1.0 1.0";

        [JsonProperty("material")]
        public string Material { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("png")]
        public string Png { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiButtonComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Button";

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("close")]
        public string Close { get; set; }

        //The sprite that is used to render this image.
        [DefaultValue("Assets/Content/UI/UI.Background.Tile.psd")]
        [JsonProperty("sprite")]
        public string Sprite { get; set; } = "Assets/Content/UI/UI.Background.Tile.psd";

        //The Material set by the player.
        [DefaultValue("Assets/Icons/IconMaterial.mat")]
        [JsonProperty("material")]
        public string Material { get; set; } = "Assets/Icons/IconMaterial.mat";

        public string Color { get; set; } = "1.0 1.0 1.0 1.0";

        //How the Image is draw.
        [DefaultValue(Image.Type.Simple)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("imagetype")]
        public Image.Type ImageType { get; set; } = Image.Type.Simple;

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiOutlineComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Outline";

        //Color for the effect.
        public string Color { get; set; } = "1.0 1.0 1.0 1.0";

        //How far is the shadow from the graphic.
        [DefaultValue("1.0 -1.0")]
        [JsonProperty("distance")]
        public string Distance { get; set; } = "1.0 -1.0";

        //Should the shadow inherit the alpha from the graphic?
        [DefaultValue(false)]
        [JsonProperty("useGraphicAlpha")]
        public bool UseGraphicAlpha { get; set; }
    }

    public class CuiInputFieldComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.InputField";

        //The string value this text will display.
        [DefaultValue("Text")]
        [JsonProperty("text")]
        public string Text { get; set; } = "Text";

        //The size that the Font should render at.
        [DefaultValue(14)]
        [JsonProperty("fontSize")]
        public int FontSize { get; set; } = 14;

        //The Font used by the text.
        [DefaultValue("RobotoCondensed-Bold.ttf")]
        [JsonProperty("font")]
        public string Font { get; set; } = "RobotoCondensed-Bold.ttf";

        //The positioning of the text reliative to its RectTransform.
        [DefaultValue(TextAnchor.UpperLeft)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("align")]
        public TextAnchor Align { get; set; } = TextAnchor.UpperLeft;

        public string Color { get; set; } = "1.0 1.0 1.0 1.0";

        [DefaultValue(100)]
        [JsonProperty("characterLimit")]
        public int CharsLimit { get; set; } = 100;

        [JsonProperty("command")]
        public string Command { get; set; }

        [DefaultValue(false)]
        [JsonProperty("password")]
        public bool IsPassword { get; set; }
    }

    public class CuiNeedsCursorComponent : ICuiComponent
    {
        public string Type => "NeedsCursor";
    }

    public class CuiRectTransformComponent : ICuiComponent
    {
        public string Type => "RectTransform";

        //The normalized position in the parent RectTransform that the lower left corner is anchored to.
        [DefaultValue("0.0 0.0")]
        [JsonProperty("anchormin")]
        public string AnchorMin { get; set; } = "0.0 0.0";

        //The normalized position in the parent RectTransform that the upper right corner is anchored to.
        [DefaultValue("1.0 1.0")]
        [JsonProperty("anchormax")]
        public string AnchorMax { get; set; } = "1.0 1.0";

        //The offset of the lower left corner of the rectangle relative to the lower left anchor.
        [DefaultValue("0.0 0.0")]
        [JsonProperty("offsetmin")]
        public string OffsetMin { get; set; } = "0.0 0.0";

        //The offset of the upper right corner of the rectangle relative to the upper right anchor.
        [DefaultValue("1.0 1.0")]
        [JsonProperty("offsetmax")]
        public string OffsetMax { get; set; } = "1.0 1.0";
    }

    public class ComponentConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            var typeName = jObject["type"].ToString();
            Type type;
            switch (typeName)
            {
                case "UnityEngine.UI.Text":
                    type = typeof(CuiTextComponent);
                    break;

                case "UnityEngine.UI.Image":
                    type = typeof(CuiImageComponent);
                    break;

                case "UnityEngine.UI.RawImage":
                    type = typeof(CuiRawImageComponent);
                    break;

                case "UnityEngine.UI.Button":
                    type = typeof(CuiButtonComponent);
                    break;

                case "UnityEngine.UI.Outline":
                    type = typeof(CuiOutlineComponent);
                    break;

                case "UnityEngine.UI.InputField":
                    type = typeof(CuiInputFieldComponent);
                    break;

                case "NeedsCursor":
                    type = typeof(CuiNeedsCursorComponent);
                    break;

                case "RectTransform":
                    type = typeof(CuiRectTransformComponent);
                    break;

                default:
                    return null;
            }
            var target = Activator.CreateInstance(type);
            serializer.Populate(jObject.CreateReader(), target);
            return target;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(ICuiComponent);

        public override bool CanWrite => false;
    }

    public class CuiColor
    {
        public byte Red { get; set; } = 0;
        public byte Green { get; set; } = 0;
        public byte Blue { get; set; } = 0;
        public float Alpha { get; set; } = 0f;

        public CuiColor() { }

        public CuiColor(byte red, byte green, byte blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public override string ToString() => $"{(double)Red / 255} {(double)Green / 255} {(double)Blue / 255} {Alpha}";
    }

    public class CuiRect
    {
        public float Top { get; set; } = 0f;
        public float Bottom { get; set; } = 0f;
        public float Left { get; set; } = 0f;
        public float Right { get; set; } = 0f;

        public CuiRect() { }

        public CuiRect(float top, float bottom, float left, float right)
        {
            Top = top;
            Bottom = bottom;
            Left = left;
            Right = right;
        }

        public string GetPosMin() => $"{Left} {Bottom}";
        public string GetPosMax() => $"{Right} {Top}";
    }

    public class CuiDefaultColors
    {
        public static CuiColor Black { get; } = new CuiColor() { Red = 0, Green = 0, Blue = 0, Alpha = 1f };
        public static CuiColor Maroon { get; } = new CuiColor() { Red = 128, Green = 0, Blue = 0, Alpha = 1f };
        public static CuiColor Green { get; } = new CuiColor() { Red = 0, Green = 128, Blue = 0, Alpha = 1f };
        public static CuiColor Olive { get; } = new CuiColor() { Red = 128, Green = 128, Blue = 0, Alpha = 1f };
        public static CuiColor Navy { get; } = new CuiColor() { Red = 0, Green = 0, Blue = 128, Alpha = 1f };
        public static CuiColor Purple { get; } = new CuiColor() { Red = 128, Green = 0, Blue = 128, Alpha = 1f };
        public static CuiColor Teal { get; } = new CuiColor() { Red = 0, Green = 128, Blue = 128, Alpha = 1f };
        public static CuiColor Gray { get; } = new CuiColor() { Red = 128, Green = 128, Blue = 128, Alpha = 1f };
        public static CuiColor Silver { get; } = new CuiColor() { Red = 192, Green = 192, Blue = 192, Alpha = 1f };
        public static CuiColor Red { get; } = new CuiColor() { Red = 255, Green = 0, Blue = 0, Alpha = 1f };
        public static CuiColor Lime { get; } = new CuiColor() { Red = 0, Green = 255, Blue = 0, Alpha = 1f };
        public static CuiColor Yellow { get; } = new CuiColor() { Red = 255, Green = 255, Blue = 0, Alpha = 1f };
        public static CuiColor Blue { get; } = new CuiColor() { Red = 0, Green = 0, Blue = 255, Alpha = 1f };
        public static CuiColor Fuchsia { get; } = new CuiColor() { Red = 255, Green = 0, Blue = 255, Alpha = 1f };
        public static CuiColor Aqua { get; } = new CuiColor() { Red = 0, Green = 255, Blue = 255, Alpha = 1f };
        public static CuiColor LightGray { get; } = new CuiColor() { Red = 211, Green = 211, Blue = 211, Alpha = 1f };
        public static CuiColor MediumGray { get; } = new CuiColor() { Red = 160, Green = 160, Blue = 164, Alpha = 1f };
        public static CuiColor DarkGray { get; } = new CuiColor() { Red = 169, Green = 169, Blue = 169, Alpha = 1f };
        public static CuiColor White { get; } = new CuiColor() { Red = 255, Green = 255, Blue = 255, Alpha = 1f };
        public static CuiColor MoneyGreen { get; } = new CuiColor() { Red = 192, Green = 220, Blue = 192, Alpha = 1f };
        public static CuiColor SkyBlue { get; } = new CuiColor() { Red = 166, Green = 202, Blue = 240, Alpha = 1f };
        public static CuiColor Cream { get; } = new CuiColor() { Red = 255, Green = 251, Blue = 240, Alpha = 1f };

        public static CuiColor Background { get; } = new CuiColor() { Red = 240, Green = 240, Blue = 240, Alpha = 0.3f };
        public static CuiColor BackgroundMedium { get; } = new CuiColor() { Red = 76, Green = 74, Blue = 72, Alpha = 0.83f };
        public static CuiColor BackgroundDark { get; } = new CuiColor() { Red = 42, Green = 42, Blue = 42, Alpha = 0.93f };

        public static CuiColor Button { get; } = new CuiColor() { Red = 42, Green = 42, Blue = 42, Alpha = 0.9f };
        public static CuiColor ButtonInactive { get; } = new CuiColor() { Red = 168, Green = 168, Blue = 168, Alpha = 0.9f };
        public static CuiColor ButtonAccept { get; } = new CuiColor() { Red = 0, Green = 192, Blue = 0, Alpha = 0.9f };
        public static CuiColor ButtonDecline { get; } = new CuiColor() { Red = 192, Green = 0, Blue = 0, Alpha = 0.9f };

        public static CuiColor Text { get; } = new CuiColor() { Red = 0, Green = 0, Blue = 0, Alpha = 1f };
        public static CuiColor TextAlt { get; } = new CuiColor() { Red = 255, Green = 255, Blue = 255, Alpha = 1f };
        public static CuiColor TextMuted { get; } = new CuiColor() { Red = 147, Green = 147, Blue = 147, Alpha = 1f };
        public static CuiColor TextTitle { get; } = new CuiColor() { Red = 206, Green = 66, Blue = 43, Alpha = 1f };

        public static CuiColor None { get; } = new CuiColor() { Red = 0, Green = 0, Blue = 0, Alpha = 0f };
    }

    public class Cui
    {
        public const string PARENT_HUD = "Hud";
        public const string PARENT_OVERLAY = "Overlay";

        public string MainPanelName { get; set; }
        private CuiElementContainer container = new CuiElementContainer();

        public string AddPanel(string parent,
                               CuiRect anchor,
                               bool cursorEnabled,
                               CuiColor color = null,
                               Image.Type imageType = Image.Type.Simple,
                               string sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                               string material = "Assets/Icons/IconMaterial.mat",
                               string png = "",
                               float fadeOut = 0f,
                               float fadeIn = 0f)
        {
            CuiRect offset = new CuiRect();
            return AddRawPanel(parent, anchor, offset, cursorEnabled, color, null, imageType, sprite, material, "", "", png, "", "", fadeOut, fadeIn, 0f);
        }

        public string AddPanel(string parent,
                               CuiRect anchor,
                               CuiRect offset,
                               bool cursorEnabled,
                               CuiColor color = null,
                               Image.Type imageType = Image.Type.Simple,
                               string sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                               string material = "Assets/Icons/IconMaterial.mat",
                               string png = "",
                               float fadeOut = 0f,
                               float fadeIn = 0f)
        {
            return AddRawPanel(parent, anchor, offset, cursorEnabled, color, null, imageType, sprite, material, "", "", png, "", "", fadeOut, fadeIn, 0f);
        }

        public string AddRawPanel(string parent,
                                  CuiRect anchor,
                                  bool cursorEnabled,
                                  CuiColor color = null,
                                  CuiColor rawColor = null,
                                  Image.Type imageType = Image.Type.Simple,
                                  string sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                                  string material = "Assets/Icons/IconMaterial.mat",
                                  string rawSprite = "Assets/Icons/rust.png",
                                  string rawMaterial = "",
                                  string png = "",
                                  string rawPng = "",
                                  string rawUrl = "",
                                  float fadeOut = 0f,
                                  float fadeIn = 0f,
                                  float rawFadeIn = 0f)
        {
            CuiRect offset = new CuiRect();
            return AddRawPanel(parent, anchor, offset, cursorEnabled, color, rawColor, imageType, sprite, material, rawSprite, rawMaterial, png, rawPng, rawUrl, fadeOut, fadeIn, rawFadeIn);
        }

        public string AddRawPanel(string parent,
                                  CuiRect anchor,
                                  CuiRect offset,
                                  bool cursorEnabled,
                                  CuiColor color = null,
                                  CuiColor rawColor = null,
                                  Image.Type imageType = Image.Type.Simple,
                                  string png = "",
                                  string rawPng = "",
                                  string rawUrl = "",
                                  string sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                                  string material = "Assets/Icons/IconMaterial.mat",
                                  string rawSprite = "Assets/Icons/rust.png",
                                  string rawMaterial = "",
                                  float fadeOut = 0f,
                                  float fadeIn = 0f,
                                  float rawFadeIn = 0f)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchor.GetPosMin(),
                    AnchorMax = anchor.GetPosMax(),
                    OffsetMin = offset.GetPosMin(),
                    OffsetMax = offset.GetPosMax()
                },
                CursorEnabled = cursorEnabled,
                FadeOut = fadeOut
            };

            if (!string.IsNullOrEmpty(sprite) ||
                !string.IsNullOrEmpty(material) ||
                !string.IsNullOrEmpty(png) ||
                (color != null)) {
                panel.Image = new CuiImageComponent
                {
                    Sprite = sprite ?? "",
                    Material = material ?? "",
                    Color = color.ToString(),
                    ImageType = imageType,
                    Png = png ?? "",
                    FadeIn = fadeIn
                };
            }

            if (!string.IsNullOrEmpty(rawSprite) ||
                !string.IsNullOrEmpty(rawMaterial) ||
                !string.IsNullOrEmpty(rawUrl) ||
                !string.IsNullOrEmpty(rawPng) ||
                (rawColor != null)) {
                panel.RawImage = new CuiRawImageComponent
                {
                    Sprite = rawSprite ?? "",
                    Material = rawMaterial ?? "",
                    Color = rawColor.ToString(),
                    Url = rawUrl ?? "",
                    Png = rawPng ?? "",
                    FadeIn = rawFadeIn
                };
            }

            return container.Add(panel, parent);
        }

        public string AddLabel(string parent,
                               CuiRect anchor,
                               CuiColor color,
                               string text,
                               int fontSize = 14,
                               TextAnchor align = TextAnchor.UpperLeft,
                               string font = "RobotoCondensed-Bold.ttf",
                               float fadeOut = 0f,
                               float fadeIn = 0f)
        {
            CuiRect offset = new CuiRect();
            return AddLabel(parent, anchor, offset, color, text, fontSize, align, font, fadeOut, fadeIn);
        }

        public string AddLabel(string parent,
                               CuiRect anchor,
                               CuiRect offset,
                               CuiColor color,
                               string text,
                               int fontSize = 14,
                               TextAnchor align = TextAnchor.UpperLeft,
                               string font = "RobotoCondensed-Bold.ttf",
                               float fadeOut = 0f,
                               float fadeIn = 0f)
        {
            return container.Add(new CuiLabel
            {
                Text =
                {
                    Text = text ?? "",
                    FontSize = fontSize,
                    Font = font ?? "RobotoCondensed-Bold.ttf",
                    Align = align,
                    Color = color.ToString(),
                    FadeIn = fadeIn
                },
                RectTransform =
                {
                    AnchorMin = anchor.GetPosMin(),
                    AnchorMax = anchor.GetPosMax(),
                    OffsetMin = offset.GetPosMin(),
                    OffsetMax = offset.GetPosMax()
                },
                FadeOut = fadeOut
            }, parent);
        }

        public string AddButton(string parent,
                                CuiRect anchor,
                                CuiColor buttonColor,
                                CuiColor textColor,
                                string text,
                                string command = "",
                                string close = "",
                                int fontSize = 14,
                                TextAnchor align = TextAnchor.MiddleCenter,
                                string font = "RobotoCondensed-Bold.ttf",
                                Image.Type imageType = Image.Type.Simple,
                                string sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                                string material = "Assets/Icons/IconMaterial.mat",
                                float fadeOut = 0f,
                                float fadeIn = 0f)
        {
            CuiRect offset = new CuiRect();
            return AddButton(parent, anchor, offset, buttonColor, textColor, text, command, close, fontSize, align, font, imageType, sprite, material, fadeOut, fadeIn);
        }

        public string AddButton(string parent,
                                CuiRect anchor,
                                CuiRect offset,
                                CuiColor buttonColor,
                                CuiColor textColor,
                                string text,
                                string command = "",
                                string close = "",
                                int fontSize = 14,
                                TextAnchor align = TextAnchor.MiddleCenter,
                                string font = "RobotoCondensed-Bold.ttf",
                                Image.Type imageType = Image.Type.Simple,
                                string sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                                string material = "Assets/Icons/IconMaterial.mat",
                                float fadeOut = 0f,
                                float fadeIn = 0f)
        {
            return container.Add(new CuiButton
            {
                Button =
                {
                    Command = command ?? "",
                    Close = close ?? "",
                    Sprite = sprite ?? "",
                    Material = material ?? "",
                    Color = buttonColor.ToString(),
                    ImageType = imageType,
                    FadeIn = fadeIn
                },
                RectTransform =
                {
                    AnchorMin = anchor.GetPosMin(),
                    AnchorMax = anchor.GetPosMax(),
                    OffsetMin = offset.GetPosMin(),
                    OffsetMax = offset.GetPosMax()
                },
                Text =
                {
                    Text = text ?? "",
                    FontSize = fontSize,
                    Font = font ?? "RobotoCondensed-Bold.ttf",
                    Align = align,
                    Color = textColor.ToString(),
                    FadeIn = fadeIn
                },
                FadeOut = fadeOut
            }, parent);
        }

        public string AddInputField(string parent,
                                    CuiRect anchor,
                                    CuiColor color,
                                    string text = "",
                                    string command = "",
                                    int fontSize = 14,
                                    TextAnchor align = TextAnchor.MiddleLeft,
                                    string font = "RobotoCondensed-Bold.ttf",
                                    float fadeOut = 0f)
        {
            CuiRect offset = new CuiRect();
            return AddInputField(parent, anchor, offset, color, text, command, fontSize, align, font, fadeOut);
        }

        public string AddInputField(string parent,
                                    CuiRect anchor,
                                    CuiRect offset,
                                    CuiColor color,
                                    string text = "",
                                    string command = "",
                                    int fontSize = 14,
                                    TextAnchor align = TextAnchor.MiddleLeft,
                                    string font = "RobotoCondensed-Bold.ttf",
                                    float fadeOut = 0f)
        {
            return container.Add(new CuiInputField
            {
                InputField =
                {
                    Text = text ?? "",
                    FontSize = fontSize,
                    Font = font ?? "RobotoCondensed-Bold.ttf",
                    Align = align,
                    Color = color.ToString(),
                    CharsLimit = 100,
                    Command = command ?? "",
                    IsPassword = false
                },
                RectTransform =
                {
                    AnchorMin = anchor.GetPosMin(),
                    AnchorMax = anchor.GetPosMax(),
                    OffsetMin = offset.GetPosMin(),
                    OffsetMax = offset.GetPosMax()
                },
                FadeOut = fadeOut
            }, parent);
        }

        public string AddComponent(CuiButton Component, string parent = "Hud", string name = null) => container.Add(Component, parent, name);
        public string AddComponent(CuiLabel Component, string parent = "Hud", string name = null) => container.Add(Component, parent, name);
        public string AddComponent(CuiInputField Component, string parent = "Hud", string name = null) => container.Add(Component, parent, name);
        public string AddComponent(CuiPanel Component, string parent = "Hud", string name = null) => container.Add(Component, parent, name);

        public bool Draw(BasePlayer player)
        {
            if (!string.IsNullOrEmpty(MainPanelName))
                return CuiHelper.AddUi(player, container);

            return false;
        }

        public bool Destroy(BasePlayer player)
        {
            if (!string.IsNullOrEmpty(MainPanelName))
                return CuiHelper.DestroyUi(player, MainPanelName);

            return false;
        }
    }
}
