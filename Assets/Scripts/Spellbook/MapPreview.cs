using UnityEngine;
using UnityEngine.UI;

// Renders a BattleMap exactly the way BattleStage.ApplyMap lays it out (Far / Middle / Ground),
// but as UI images so the Spellbook previews the same artwork the battle uses.
public static class MapPreview {
    const float ViewHeight = BattleStage.CameraHeight * 2f;

    // `frame` should carry the mask. `aspect` is the frame's width / height.
    public static RectTransform Build(RectTransform frame, BattleMap map, float aspect) {
        var view = new GameObject("Map " + map.id, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        view.SetParent(frame, false);
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = view.offsetMax = Vector2.zero;
        view.pivot = new Vector2(.5f, .5f);

        var sky = new GameObject("Sky", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        sky.rectTransform.SetParent(view, false);
        Stretch(sky.rectTransform);
        sky.color = map.sky; sky.raycastTarget = false;

        var far = Resources.Load<Sprite>(map.Art("Far"));
        var ground = Resources.Load<Sprite>(map.Art("Ground"));
        if (far == null || ground == null) return view;

        float viewWidth = ViewHeight * aspect;
        float band = Mathf.Max(.08f, 1f - map.groundLine);
        float groundHeight = ground.rect.height * (BattleStage.StageWidth / ground.rect.width);
        float scale = Mathf.Max(1f, 2.85f / (band * groundHeight), 5.55f / (band * groundHeight));
        float groundWidth = BattleStage.StageWidth * scale;
        groundHeight *= scale;
        float groundBottom = BattleStage.Horizon - groundHeight * band;

        float unit = BattleStage.StageWidth / Mathf.Max(far.rect.width, ground.rect.width);
        float farWidth = far.rect.width * unit, farHeight = far.rect.height * unit;
        float cover = Mathf.Max(26.8f / farWidth, (BattleStage.CameraHeight * 2f + 1.5f) / farHeight, 1f);
        farWidth *= cover; farHeight *= cover;
        float farBottom = BattleStage.CameraHeight + .75f - farHeight;
        if (farBottom > BattleStage.Horizon - .3f) farBottom = BattleStage.Horizon - .3f;
        Layer(view, "Far", far, viewWidth, farWidth, farBottom);

        float pixel = groundWidth / ground.rect.width;
        for (int i = 0; i < map.middle.Length; i++) {
            var sprite = Resources.Load<Sprite>(map.Art(map.middle[i]));
            if (sprite == null) continue;
            float width = sprite.rect.width * pixel, height = sprite.rect.height * pixel;
            float bottom = map.pinToGround[i] ? groundBottom : BattleStage.Horizon - .04f - height * (1f - map.middleBottom[i]);
            if (map.pinToGround[i] && width >= groundWidth * .7f) {
                float needTop = BattleStage.CameraHeight + .9f;
                if (bottom + height < needTop) {
                    float grow = (needTop - bottom) / Mathf.Max(.01f, height);
                    width *= grow; height *= grow;
                }
            }
            Layer(view, "Middle " + i, sprite, viewWidth, width, bottom);
        }
        Layer(view, "Ground", ground, viewWidth, groundWidth, groundBottom);
        return view;
    }

    static void Layer(RectTransform view, string name, Sprite sprite, float viewWidth, float width, float bottom) {
        float height = width * sprite.rect.height / sprite.rect.width;
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = image.rectTransform;
        rect.SetParent(view, false);
        rect.anchorMin = new Vector2((-width * .5f + viewWidth * .5f) / viewWidth, (bottom + ViewHeight * .5f) / ViewHeight);
        rect.anchorMax = new Vector2((width * .5f + viewWidth * .5f) / viewWidth, (bottom + height + ViewHeight * .5f) / ViewHeight);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.sprite = sprite; image.raycastTarget = false;
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
