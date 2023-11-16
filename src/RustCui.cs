extern alias References;

using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Game.Rust.Cui
{
    public static class CuiHelper
    {
        public static string ToJson(List<CuiElement> elements, bool format = false)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = format,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(elements, options).Replace("\\n", "\n");
        }

        public static List<CuiElement> FromJson(string json) => JsonSerializer.Deserialize<List<CuiElement>>(json);

        public static string GetGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);

        public static bool AddUi(BasePlayer player, List<CuiElement> elements) => AddUi(player, ToJson(elements));

        public static bool AddUi(BasePlayer player, string json)
        {
            if (player?.net != null && Interface.CallHook("CanUseUI", player, json) == null)
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", json);
                return true;
            }

            return false;
        }

        public static bool DestroyUi(BasePlayer player, string elem)
        {
            if (player?.net != null)
            {
                Interface.CallHook("OnDestroyUI", player, elem);
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", elem);
                return true;
            }

            return false;
        }

        public static void SetColor(this ICuiColor elem, Color color)
        {
            elem.Color = $"{color.r} {color.g} {color.b} {color.a}";
        }

        public static Color GetColor(this ICuiColor elem) => ColorEx.Parse(elem.Color);
    }

    public class CuiElementContainer : List<CuiElement>
    {
        public string Add(CuiButton button, string parent = "Hud", string name = null, string destroyUi = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = CuiHelper.GetGuid();
            }

            Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = button.FadeOut,
                DestroyUi = destroyUi,
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

        public string Add(CuiLabel label, string parent = "Hud", string name = null, string destroyUi = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = CuiHelper.GetGuid();
            }

            Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = label.FadeOut,
                DestroyUi = destroyUi,
                Components =
                {
                    label.Text,
                    label.RectTransform
                }
            });
            return name;
        }

        public string Add(CuiPanel panel, string parent = "Hud", string name = null, string destroyUi = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = CuiHelper.GetGuid();
            }

            CuiElement element = new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = panel.FadeOut,
                DestroyUi = destroyUi
            };

            if (panel.Image != null)
            {
                element.Components.Add(panel.Image);
            }

            if (panel.RawImage != null)
            {
                element.Components.Add(panel.RawImage);
            }

            element.Components.Add(panel.RectTransform);

            if (panel.CursorEnabled)
            {
                element.Components.Add(new CuiNeedsCursorComponent());
            }

            if (panel.KeyboardEnabled)
            {
                element.Components.Add(new CuiNeedsKeyboardComponent());
            }

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
        public bool KeyboardEnabled { get; set; }
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
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("parent")]
        public string Parent { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("destroyUi")]
        public string DestroyUi { get; set; }

        [JsonPropertyName("components")]
        public List<ICuiComponent> Components { get; } = new List<ICuiComponent>();

        [JsonPropertyName("fadeOut")]
        public float FadeOut { get; set; }

        [JsonPropertyName("update")]
        public bool Update { get; set; }
    }

    [JsonConverter(typeof(ComponentConverter))]
    public interface ICuiComponent
    {
        [JsonPropertyName("type")]
        string Type { get; }
    }

    public interface ICuiColor
    {
        [JsonPropertyName("color")]
        string Color { get; set; }
    }

    public class CuiTextComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Text";

        // The string value this text will display.
        [JsonPropertyName("text")]
        public string Text { get; set; }

        // The size that the Font should render at
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; }

        // The Font used by the text
        [JsonPropertyName("font")]
        public string Font { get; set; }

        // The positioning of the text relative to its RectTransform
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("align")]
        public TextAnchor Align { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("verticalOverflow")]
        public VerticalWrapMode VerticalOverflow { get; set; }

        [JsonPropertyName("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiImageComponent : ICuiComponent, ICuiColor
    {
        [JsonPropertyName("type")]
        public string Type => "UnityEngine.UI.Image";

        [JsonPropertyName("sprite")]
        public string Sprite { get; set; }

        [JsonPropertyName("material")]
        public string Material { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("imagetype")]
        public Image.Type ImageType { get; set; }

        [JsonPropertyName("png")]
        public string Png { get; set; }

        [JsonPropertyName("fadeIn")]
        public float FadeIn { get; set; }

        [JsonPropertyName("itemid")]
        public int ItemId { get; set; }

        [JsonPropertyName("skinid")]
        public ulong SkinId { get; set; }
    }

    public class CuiRawImageComponent : ICuiComponent, ICuiColor
    {
        [JsonPropertyName("type")]
        public string Type => "UnityEngine.UI.RawImage";

        [JsonPropertyName("sprite")]
        public string Sprite { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("material")]
        public string Material { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("png")]
        public string Png { get; set; }

        [JsonPropertyName("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiButtonComponent : ICuiComponent, ICuiColor
    {
        [JsonPropertyName("type")]
        public string Type => "UnityEngine.UI.Button";

        [JsonPropertyName("command")]
        public string Command { get; set; }

        [JsonPropertyName("close")]
        public string Close { get; set; }

        // The sprite that is used to render this image
        [JsonPropertyName("sprite")]
        public string Sprite { get; set; }

        // The Material set by the player
        [JsonPropertyName("material")]
        public string Material { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        // How the Image is draw
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("imagetype")]
        public Image.Type ImageType { get; set; }

        [JsonPropertyName("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiOutlineComponent : ICuiComponent, ICuiColor
    {
        [JsonPropertyName("type")]
        public string Type => "UnityEngine.UI.Outline";

        [JsonPropertyName("color")]
        public string Color { get; set; }

        // How far is the shadow from the graphic
        [JsonPropertyName("distance")]
        public string Distance { get; set; }

        // Should the shadow inherit the alpha from the graphic
        [JsonPropertyName("useGraphicAlpha")]
        public bool UseGraphicAlpha { get; set; }
    }

    public class CuiInputFieldComponent : ICuiComponent, ICuiColor
    {
        [JsonPropertyName("type")]
        public string Type => "UnityEngine.UI.InputField";

        // The string value this text will display
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        // The size that the Font should render at
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; }

        // The Font used by the text
        [JsonPropertyName("font")]
        public string Font { get; set; }

        // The positioning of the text relative to its RectTransform
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("align")]
        public TextAnchor Align { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("characterLimit")]
        public int CharsLimit { get; set; }

        [JsonPropertyName("command")]
        public string Command { get; set; }

        [JsonPropertyName("password")]
        public bool IsPassword { get; set; }

        [JsonPropertyName("readOnly")]
        public bool ReadOnly { get; set; }

        [JsonPropertyName("needsKeyboard")]
        public bool NeedsKeyboard { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("lineType")]
        public InputField.LineType LineType { get; set; }

        [JsonPropertyName("autofocus")]
        public bool Autofocus { get; set; }

        [JsonPropertyName("hudMenuInput")]
        public bool HudMenuInput { get; set; }
    }

    public class CuiCountdownComponent : ICuiComponent
    {
        [JsonPropertyName("type")]
        public string Type => "Countdown";

        [JsonPropertyName("endTime")]
        public int EndTime { get; set; }

        [JsonPropertyName("startTime")]
        public int StartTime { get; set; }

        [JsonPropertyName("step")]
        public int Step { get; set; }

        [JsonPropertyName("command")]
        public string Command { get; set; }

        [JsonPropertyName("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiNeedsCursorComponent : ICuiComponent
    {
        [JsonPropertyName("type")]
        public string Type => "NeedsCursor";
    }

    public class CuiNeedsKeyboardComponent : ICuiComponent
    {
        [JsonPropertyName("type")]
        public string Type => "NeedsKeyboard";
    }

    public class CuiRectTransformComponent : ICuiComponent
    {
        [JsonPropertyName("type")]
        public string Type => "RectTransform";

        // The normalized position in the parent RectTransform that the lower left corner is anchored to
        [JsonPropertyName("anchormin")]
        public string AnchorMin { get; set; }

        // The normalized position in the parent RectTransform that the upper right corner is anchored to
        [JsonPropertyName("anchormax")]
        public string AnchorMax { get; set; }

        // The offset of the lower left corner of the rectangle relative to the lower left anchor
        [JsonPropertyName("offsetmin")]
        public string OffsetMin { get; set; }

        // The offset of the upper right corner of the rectangle relative to the upper right anchor
        [JsonPropertyName("offsetmax")]
        public string OffsetMax { get; set; }
    }

    public class ComponentConverter : JsonConverter<ICuiComponent>
    {
        public override ICuiComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = doc.RootElement;

                string typeName = root.GetProperty("type").GetString();
                Type type = null;

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
                    case "Countdown":
                        type = typeof(CuiCountdownComponent);
                        break;
                    case "NeedsCursor":
                        type = typeof(CuiNeedsCursorComponent);
                        break;
                    case "NeedsKeyboard":
                        type = typeof(CuiNeedsKeyboardComponent);
                        break;
                    case "RectTransform":
                        type = typeof(CuiRectTransformComponent);
                        break;
                    default:
                        return null;
                }

                var jsonString = root.GetRawText();
                return (ICuiComponent)JsonSerializer.Deserialize(jsonString, type, options);
            }
        }

        public override void Write(Utf8JsonWriter writer, ICuiComponent value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }

        public override bool CanConvert(Type typeToConvert) => typeof(ICuiComponent).IsAssignableFrom(typeToConvert);
    }
}
