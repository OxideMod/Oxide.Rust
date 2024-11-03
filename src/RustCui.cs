extern alias References;

using Oxide.Core;
using References::Newtonsoft.Json;
using References::Newtonsoft.Json.Converters;
using References::Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Game.Rust.Cui
{
    public static class CuiHelper
    {
        public static string ToJson(List<CuiElement> elements, bool format = false)
        {
            return JsonConvert.SerializeObject(elements, format ? Formatting.Indented : Formatting.None, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            }).Replace("\\n", "\n");
        }

        public static List<CuiElement> FromJson(string json) => JsonConvert.DeserializeObject<List<CuiElement>>(json);

        public static string GetGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);

        public static bool AddUi(BasePlayer player, List<CuiElement> elements) => AddUi(player, ToJson(elements));

        public static bool AddUi(BasePlayer player, string json)
        {
            if (player?.net != null && Interface.CallHook("CanUseUI", player, json) == null)
            {
                CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", player.net.connection ), json);
                return true;
            }

            return false;
        }

        public static bool DestroyUi(BasePlayer player, string elem)
        {
            if (player?.net != null)
            {
                Interface.CallHook("OnDestroyUI", player, elem);
                CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", player.net.connection ), elem);
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
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonProperty("destroyUi", NullValueHandling=NullValueHandling.Ignore)]
        public string DestroyUi { get; set; }

        [JsonProperty("components")]
        public List<ICuiComponent> Components { get; } = new List<ICuiComponent>();

        [JsonProperty("fadeOut")]
        public float FadeOut { get; set; }

        [JsonProperty("update", NullValueHandling = NullValueHandling.Ignore)]
		public bool Update { get; set; }
    }

    [JsonConverter(typeof(ComponentConverter))]
    public interface ICuiComponent
    {
        [JsonProperty("type")]
        string Type { get; }
    }

    public interface ICuiColor
    {
        [JsonProperty("color")]
        string Color { get; set; }
    }

    public class CuiTextComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Text";

        // The string value this text will display.
        [JsonProperty("text")]
        public string Text { get; set; }

        // The size that the Font should render at
        [JsonProperty("fontSize")]
        public int FontSize { get; set; }

        // The Font used by the text
        [JsonProperty("font")]
        public string Font { get; set; }

        // The positioning of the text relative to its RectTransform
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("align")]
        public TextAnchor Align { get; set; }

        public string Color { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("verticalOverflow")]
        public VerticalWrapMode VerticalOverflow { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiImageComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Image";

        [JsonProperty("sprite")]
        public string Sprite { get; set; }

        [JsonProperty("material")]
        public string Material { get; set; }

        public string Color { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("imagetype")]
        public Image.Type ImageType { get; set; }

        [JsonProperty("png")]
        public string Png { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        [JsonProperty("itemid")]
        public int ItemId { get; set; }

        [JsonProperty("skinid")]
        public ulong SkinId { get; set; }

        [JsonProperty("steamid")]
        public ulong SteamId { get; set; }
    }

    public class CuiRawImageComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.RawImage";

        [JsonProperty("sprite")]
        public string Sprite { get; set; }

        public string Color { get; set; }

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

        // The sprite that is used to render this image
        [JsonProperty("sprite")]
        public string Sprite { get; set; }

        // The Material set by the player
        [JsonProperty("material")]
        public string Material { get; set; }

        public string Color { get; set; }

        // How the Image is draw
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("imagetype")]
        public Image.Type ImageType { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }
    }

    public class CuiOutlineComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.Outline";

        // Color for the effect
        public string Color { get; set; }

        // How far is the shadow from the graphic
        [JsonProperty("distance")]
        public string Distance { get; set; }

        // Should the shadow inherit the alpha from the graphic
        [JsonProperty("useGraphicAlpha")]
        public bool UseGraphicAlpha { get; set; }
    }

    public class CuiInputFieldComponent : ICuiComponent, ICuiColor
    {
        public string Type => "UnityEngine.UI.InputField";

        // The string value this text will display
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        // The size that the Font should render at
        [JsonProperty("fontSize")]
        public int FontSize { get; set; }

        // The Font used by the text
        [JsonProperty("font")]
        public string Font { get; set; }

        // The positioning of the text relative to its RectTransform
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("align")]
        public TextAnchor Align { get; set; }

        public string Color { get; set; }

        [JsonProperty("characterLimit")]
        public int CharsLimit { get; set; }

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("password")]
        public bool IsPassword { get; set; }

        [JsonProperty("readOnly")]
        public bool ReadOnly { get; set; }

        [JsonProperty("needsKeyboard")]
        public bool NeedsKeyboard { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("lineType")]
        public InputField.LineType LineType { get; set; }

        [JsonProperty("autofocus")]
        public bool Autofocus { get; set; }

        [JsonProperty("hudMenuInput")]
        public bool HudMenuInput { get; set; }
    }

    public class CuiCountdownComponent : ICuiComponent
    {
        public string Type => "Countdown";

        [JsonProperty("endTime")]
        public float EndTime { get; set; }

        [JsonProperty("startTime")]
        public float StartTime { get; set; }

        [JsonProperty("step")]
        public float Step { get; set; }

        [JsonProperty("interval")]
        public float Interval { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("timerFormat")]
        public TimerFormat TimerFormat { get; set; }

        [JsonProperty("numberFormat")]
        public string NumberFormat { get; set; }

        [JsonProperty("destroyIfDone")]
        public bool DestroyIfDone { get; set; }

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }
    }

    public enum TimerFormat
    {
        None,
        SecondsHundreth,
        MinutesSeconds,
        MinutesSecondsHundreth,
        HoursMinutes,
        HoursMinutesSeconds,
        HoursMinutesSecondsMilliseconds,
        HoursMinutesSecondsTenths,
        DaysHoursMinutes,
        DaysHoursMinutesSeconds,
        Custom
    }

    public class CuiNeedsCursorComponent : ICuiComponent
    {
        public string Type => "NeedsCursor";
    }

    public class CuiNeedsKeyboardComponent : ICuiComponent
    {
        public string Type => "NeedsKeyboard";
    }

    public class CuiRectTransformComponent : CuiRectTransform, ICuiComponent
    {
        public string Type => "RectTransform";
    }

    public class CuiRectTransform
    {
        // The normalized position in the parent RectTransform that the lower left corner is anchored to
        [JsonProperty("anchormin")]
        public string AnchorMin { get; set; }

        // The normalized position in the parent RectTransform that the upper right corner is anchored to
        [JsonProperty("anchormax")]
        public string AnchorMax { get; set; }

        // The offset of the lower left corner of the rectangle relative to the lower left anchor
        [JsonProperty("offsetmin")]
        public string OffsetMin { get; set; }

        // The offset of the upper right corner of the rectangle relative to the upper right anchor
        [JsonProperty("offsetmax")]
        public string OffsetMax { get; set; }
    }

    public class CuiScrollViewComponent : ICuiComponent
    {
        public string Type => "UnityEngine.UI.ScrollView";

        [JsonProperty("contentTransform")]
        public CuiRectTransform ContentTransform { get; set; }

        [JsonProperty("horizontal")]
        public bool Horizontal { get; set; }

        [JsonProperty("vertical")]
        public bool Vertical { get; set; }

        [JsonProperty("movementType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScrollRect.MovementType MovementType { get; set; }

        [JsonProperty("elasticity")]
        public float Elasticity { get; set; }

        [JsonProperty("inertia")]
        public bool Inertia { get; set; }

        [JsonProperty("decelerationRate")]
        public float DecelerationRate { get; set; }

        [JsonProperty("scrollSensitivity")]
        public float ScrollSensitivity { get; set; }

        [JsonProperty("horizontalScrollbar")]
        public CuiScrollbar HorizontalScrollbar { get; set; }

        [JsonProperty("verticalScrollbar")]
        public CuiScrollbar VerticalScrollbar { get; set; }
    }

    public class CuiScrollbar
    {
        [JsonProperty("invert")]
        public bool Invert { get; set; }

        [JsonProperty("autoHide")]
        public bool AutoHide { get; set; }

        [JsonProperty("handleSprite")]
        public string HandleSprite { get; set; }

        [JsonProperty("size")]
        public float Size { get; set; }

        [JsonProperty("handleColor")]
        public string HandleColor { get; set; }

        [JsonProperty("highlightColor")]
        public string HighlightColor { get; set; }

        [JsonProperty("pressedColor")]
        public string PressedColor { get; set; }

        [JsonProperty("trackSprite")]
        public string TrackSprite { get; set; }

        [JsonProperty("trackColor")]
        public string TrackColor { get; set; }
    }

    public class ComponentConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string typeName = jObject["type"].ToString();
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

                case "UnityEngine.UI.ScrollView":
                    type = typeof(CuiScrollViewComponent);
                    break;

                default:
                    return null;
            }

            object target = Activator.CreateInstance(type);
            serializer.Populate(jObject.CreateReader(), target);
            return target;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(ICuiComponent);

        public override bool CanWrite => false;
    }
}
