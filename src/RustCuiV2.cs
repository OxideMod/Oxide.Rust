extern alias Oxide;

using Oxide::Newtonsoft.Json;
using Oxide::Newtonsoft.Json.Converters;
using Oxide::Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Game.Rust.CuiV2
{
    public static class CuiHelper
    {
        public static string ToJson(List<CuiElement> elements, bool format = false)
        {
            return JsonConvert.SerializeObject(
                elements,
                format ? Formatting.Indented : Formatting.None,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore }
            ).Replace("\\n", "\n");
        }

        public static List<CuiElement> FromJson(string json) => JsonConvert.DeserializeObject<CuiElementContainer>(json);

        public static string GetGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);

        public static bool AddUi(BasePlayer player, List<CuiElement> elements) => AddUi(player, ToJson(elements));

        public static bool AddUi(BasePlayer player, string json)
        {
            if (player?.net == null)
                return false;

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", json);
            return true;
        }

        public static bool DestroyUi(BasePlayer player, string elem)
        {
            if (player?.net == null)
                return false;

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", elem);
            return true;
        }
    }

    #region Property type classes
    [JsonConverter(typeof(CuiColorConverter))]
    public class CuiColor
    {
        public byte R { get; set; } = 255;
        public byte G { get; set; } = 255;
        public byte B { get; set; } = 255;
        public float A { get; set; } = 1f;

        public CuiColor() { }

        public CuiColor(byte red, byte green, byte blue, float alpha = 1f)
        {
            R = red;
            G = green;
            B = blue;
            A = alpha;
        }

        public override string ToString() => $"{(double)R / 255} {(double)G / 255} {(double)B / 255} {A}";
    }

    [JsonConverter(typeof(CuiPointConverter))]
    public class CuiPoint
    {
        public float X { get; set; } = 0f;
        public float Y { get; set; } = 0f;

        public CuiPoint() { }

        public CuiPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"{X} {Y}";
    }
    #endregion Object classes

    #region Interfaces
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
        CuiColor Color { get; set; }
    }
    #endregion Interfaces

    #region Components
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

        public CuiColor Color { get; set; } = CuiDefaultColors.Default;

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

        public CuiColor Color { get; set; } = CuiDefaultColors.Default;

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

        public CuiColor Color { get; set; } = CuiDefaultColors.Default;

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

        public CuiColor Color { get; set; } = CuiDefaultColors.Default;

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
        public CuiColor Color { get; set; } = CuiDefaultColors.Default;

        //How far is the shadow from the graphic.
        [DefaultValue("1.0 -1.0")]
        [JsonProperty("distance")]
        public CuiPoint Distance { get; set; } = new CuiPoint(1f, -1f);

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

        public CuiColor Color { get; set; } = CuiDefaultColors.Default;

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
        public CuiPoint AnchorMin { get; set; } = new CuiPoint(0f, 0f);

        //The normalized position in the parent RectTransform that the upper right corner is anchored to.
        [DefaultValue("1.0 1.0")]
        [JsonProperty("anchormax")]
        public CuiPoint AnchorMax { get; set; } = new CuiPoint(1f, 1f);

        //The offset of the lower left corner of the rectangle relative to the lower left anchor.
        [DefaultValue("0.0 0.0")]
        [JsonProperty("offsetmin")]
        public CuiPoint OffsetMin { get; set; } = new CuiPoint(0f, 0f);

        //The offset of the upper right corner of the rectangle relative to the upper right anchor.
        [DefaultValue("1.0 1.0")]
        [JsonProperty("offsetmax")]
        public CuiPoint OffsetMax { get; set; } = new CuiPoint(1f, 1f);
    }
    #endregion Components

    #region UI object definitions
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
    #endregion UI object definitions

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

    public class CuiElementContainer : List<CuiElement>
    {
        public string Add(CuiButton button, string parent = "Hud", string name = null)
        {
            if (string.IsNullOrEmpty(name))
                name = CuiHelper.GetGuid();

            Add(new CuiElement {
                Name = name,
                Parent = parent,
                FadeOut = button.FadeOut,
                Components = {
                    button.Button,
                    button.RectTransform
                }
            });

            if (!string.IsNullOrEmpty(button.Text.Text))
                Add(new CuiElement {
                    Parent = name,
                    FadeOut = button.FadeOut,
                    Components = {
                        button.Text,
                        new CuiRectTransformComponent()
                    }
                });

            return name;
        }

        public string Add(CuiLabel label, string parent = "Hud", string name = null)
        {
            if (string.IsNullOrEmpty(name))
                name = CuiHelper.GetGuid();

            Add(new CuiElement {
                Name = name,
                Parent = parent,
                FadeOut = label.FadeOut,
                Components = {
                    label.Text,
                    label.RectTransform
                }
            });

            return name;
        }

        public string Add(CuiPanel panel, string parent = "Hud", string name = null)
        {
            if (string.IsNullOrEmpty(name))
                name = CuiHelper.GetGuid();

            var element = new CuiElement {
                Name = name,
                Parent = parent,
                FadeOut = panel.FadeOut
            };

            if (panel.Image != null)
                element.Components.Add(panel.Image);
            if (panel.RawImage != null)
                element.Components.Add(panel.RawImage);

            element.Components.Add(panel.RectTransform);

            if (panel.CursorEnabled)
                element.Components.Add(new CuiNeedsCursorComponent());

            Add(element);
            return name;
        }

        public string ToJson() => ToString();

        public override string ToString() => CuiHelper.ToJson(this);
    }

    #region Converters
    public class CuiColorConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(CuiColor);
        public override bool CanWrite => true;
        public override bool CanRead => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String)
                throw new JsonReaderException($"Value is of the wrong type: {reader.TokenType}");

            var jValue = new JValue(reader.Value);
            string[] values = ((string)jValue).Trim().Split(' ');
            float red;
            float green;
            float blue;
            float alpha;

            if (values.Length != 4)
                throw new JsonReaderException($"Value has {values.Length} items, rather then the required 4");
            
            if (!float.TryParse(values[0], out red) ||
                !float.TryParse(values[1], out green) ||
                !float.TryParse(values[2], out blue) ||
                !float.TryParse(values[3], out alpha))
                throw new JsonReaderException($"Value has one or more items that do not parse to float");

            return new CuiColor {
                R = (byte)Math.Round(red * 255),
                G = (byte)Math.Round(green * 255),
                B = (byte)Math.Round(blue * 255),
                A = alpha
            };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value.GetType() != typeof(CuiColor))
                throw new JsonWriterException("Value is not of the CuiColor type");

            var val = (CuiColor)value;
            writer.WriteValue($"{(double)val.R / 255} {(double)val.G / 255} {(double)val.B / 255} {val.A}");
        }
    }

    public class CuiPointConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(CuiColor);
        public override bool CanWrite => true;
        public override bool CanRead => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String)
                throw new JsonReaderException($"Value is of the wrong type: {reader.TokenType}");

            var jValue = new JValue(reader.Value);
            string[] values = ((string)jValue).Trim().Split(' ');
            float x;
            float y;

            if (values.Length != 2)
                throw new JsonReaderException($"Value has {values.Length} items, rather then the required 2");

            if (!float.TryParse(values[0], out x) ||
                !float.TryParse(values[1], out y))
                throw new JsonReaderException($"Value has one or more items that do not parse to float");

            return new CuiPoint {
                X = x,
                Y = y
            };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value.GetType() != typeof(CuiPoint))
                throw new JsonWriterException("Value is not of the CuiPoint type");

            var val = (CuiPoint)value;
            writer.WriteValue($"{val.X} {val.Y}");
        }
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
            switch (typeName) {
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
    #endregion Converters

    #region Defaults
    public static class CuiDefaultColors
    {
        public static CuiColor Black { get; } = new CuiColor() { R = 0, G = 0, B = 0, A = 1f };
        public static CuiColor Maroon { get; } = new CuiColor() { R = 128, G = 0, B = 0, A = 1f };
        public static CuiColor Green { get; } = new CuiColor() { R = 0, G = 128, B = 0, A = 1f };
        public static CuiColor Olive { get; } = new CuiColor() { R = 128, G = 128, B = 0, A = 1f };
        public static CuiColor Navy { get; } = new CuiColor() { R = 0, G = 0, B = 128, A = 1f };
        public static CuiColor Purple { get; } = new CuiColor() { R = 128, G = 0, B = 128, A = 1f };
        public static CuiColor Teal { get; } = new CuiColor() { R = 0, G = 128, B = 128, A = 1f };
        public static CuiColor Gray { get; } = new CuiColor() { R = 128, G = 128, B = 128, A = 1f };
        public static CuiColor Silver { get; } = new CuiColor() { R = 192, G = 192, B = 192, A = 1f };
        public static CuiColor Red { get; } = new CuiColor() { R = 255, G = 0, B = 0, A = 1f };
        public static CuiColor Lime { get; } = new CuiColor() { R = 0, G = 255, B = 0, A = 1f };
        public static CuiColor Yellow { get; } = new CuiColor() { R = 255, G = 255, B = 0, A = 1f };
        public static CuiColor Blue { get; } = new CuiColor() { R = 0, G = 0, B = 255, A = 1f };
        public static CuiColor Fuchsia { get; } = new CuiColor() { R = 255, G = 0, B = 255, A = 1f };
        public static CuiColor Aqua { get; } = new CuiColor() { R = 0, G = 255, B = 255, A = 1f };
        public static CuiColor LightGray { get; } = new CuiColor() { R = 211, G = 211, B = 211, A = 1f };
        public static CuiColor MediumGray { get; } = new CuiColor() { R = 160, G = 160, B = 164, A = 1f };
        public static CuiColor DarkGray { get; } = new CuiColor() { R = 169, G = 169, B = 169, A = 1f };
        public static CuiColor White { get; } = new CuiColor() { R = 255, G = 255, B = 255, A = 1f };
        public static CuiColor MoneyGreen { get; } = new CuiColor() { R = 192, G = 220, B = 192, A = 1f };
        public static CuiColor SkyBlue { get; } = new CuiColor() { R = 166, G = 202, B = 240, A = 1f };
        public static CuiColor Cream { get; } = new CuiColor() { R = 255, G = 251, B = 240, A = 1f };

        public static CuiColor Background { get; } = new CuiColor() { R = 240, G = 240, B = 240, A = 0.3f };
        public static CuiColor BackgroundMedium { get; } = new CuiColor() { R = 76, G = 74, B = 72, A = 0.83f };
        public static CuiColor BackgroundDark { get; } = new CuiColor() { R = 42, G = 42, B = 42, A = 0.93f };

        public static CuiColor Button { get; } = new CuiColor() { R = 42, G = 42, B = 42, A = 0.9f };
        public static CuiColor ButtonInactive { get; } = new CuiColor() { R = 168, G = 168, B = 168, A = 0.9f };
        public static CuiColor ButtonAccept { get; } = new CuiColor() { R = 0, G = 192, B = 0, A = 0.9f };
        public static CuiColor ButtonDecline { get; } = new CuiColor() { R = 192, G = 0, B = 0, A = 0.9f };

        public static CuiColor Text { get; } = new CuiColor() { R = 0, G = 0, B = 0, A = 1f };
        public static CuiColor TextAlt { get; } = new CuiColor() { R = 255, G = 255, B = 255, A = 1f };
        public static CuiColor TextMuted { get; } = new CuiColor() { R = 147, G = 147, B = 147, A = 1f };
        public static CuiColor TextTitle { get; } = new CuiColor() { R = 206, G = 66, B = 43, A = 1f };

        public static CuiColor None { get; } = new CuiColor() { R = 0, G = 0, B = 0, A = 0f };
        public static CuiColor Default { get; } = White;
    }
    #endregion
}
