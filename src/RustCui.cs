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
}
