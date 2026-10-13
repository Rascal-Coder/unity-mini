using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Small UI juice toolkit for the Spellbook: easing, tweens, particle bursts, flying icons and
// procedurally generated sprites (glow, sparkle, ring, rounded frames) so no extra art is required.
public static class UiFx {
    // ---------- easing ----------
    public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
    public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
    public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
    public static float InOutCubic(float t) { t = Mathf.Clamp01(t); return t < .5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
    public static float OutBack(float t, float s = 1.70158f) { t = Mathf.Clamp01(t) - 1f; return t * t * ((s + 1f) * t + s) + 1f; }
    public static float OutElastic(float t) {
        t = Mathf.Clamp01(t);
        if (t <= 0f || t >= 1f) return t;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - .75f) * (2f * Mathf.PI / 3f)) + 1f;
    }

    // ---------- tween ----------
    public static Coroutine Tween(MonoBehaviour host, float duration, Action<float> step, Action done = null, float delay = 0f) {
        return host.StartCoroutine(TweenRoutine(duration, step, done, delay));
    }

    static IEnumerator TweenRoutine(float duration, Action<float> step, Action done, float delay) {
        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime) { step(t / duration); yield return null; }
        step(1f);
        if (done != null) done();
    }

    public static Coroutine After(MonoBehaviour host, float delay, Action action) {
        return Tween(host, 0f, u => { }, action, delay);
    }

    // Decaying shake around the current anchored position.
    public static void Shake(MonoBehaviour host, RectTransform rect, float amplitude = 10f, float duration = .35f) {
        var origin = rect.anchoredPosition;
        Tween(host, duration, u => {
            if (rect == null) return;
            float fade = 1f - u;
            rect.anchoredPosition = origin + new Vector2(Mathf.Sin(u * 38f) * amplitude * fade, 0);
        }, () => { if (rect != null) rect.anchoredPosition = origin; });
    }

    // ---------- procedural sprites ----------
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static Sprite Build(string key, int w, int h, Func<int, int, float> alpha, Vector4 border = default(Vector4)) {
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f));
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
        cache[key] = sprite;
        return sprite;
    }

    static float Radial(int x, int y, int size) {
        float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
        return Mathf.Sqrt(u * u + v * v);
    }

    public static Sprite Glow() {
        return Build("glow", 128, 128, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Radial(x, y, 128)), 2.2f));
    }

    public static Sprite Disc() {
        return Build("disc", 256, 256, (x, y) => Mathf.Clamp01((1f - Radial(x, y, 256)) * 128f));
    }

    public static Sprite Ring() {
        return Build("ring", 256, 256, (x, y) => Mathf.Clamp01(1f - Mathf.Abs(Radial(x, y, 256) - .9f) / .09f));
    }

    public static Sprite Sparkle() {
        return Build("sparkle", 96, 96, (x, y) => {
            float u = Mathf.Abs((x + .5f) / 96f * 2f - 1f), v = Mathf.Abs((y + .5f) / 96f * 2f - 1f);
            float star = Mathf.Clamp01(1f - (Mathf.Sqrt(u) + Mathf.Sqrt(v)));
            float core = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 1.6f), 2f);
            return star * 1.5f + core;
        });
    }

    // Soft band used as a diagonal light sweep.
    public static Sprite Shine() {
        return Build("shine", 64, 8, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs((x + .5f) / 64f * 2f - 1f)), 1.6f) * .9f);
    }

    static float RoundedDistance(float px, float py, float w, float h, float r) {
        float qx = Mathf.Abs(px - w * .5f) - (w * .5f - r), qy = Mathf.Abs(py - h * .5f) - (h * .5f - r);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // 9-slice friendly rounded rectangle (fill) used for masks and plates.
    public static Sprite RoundedFill() {
        return Build("rounded-fill", 64, 64, (x, y) => .5f - RoundedDistance(x + .5f, y + .5f, 64, 64, 18), new Vector4(20, 20, 20, 20));
    }

    public static Sprite RoundedOutline() {
        return Build("rounded-outline", 64, 64, (x, y) => {
            float d = RoundedDistance(x + .5f, y + .5f, 64, 64, 18);
            return .5f - (Mathf.Abs(d + 2f) - 2f);
        }, new Vector4(20, 20, 20, 20));
    }

    // ---------- scene helpers ----------
    public static Image Add(RectTransform parent, string name, Sprite sprite, Color color, Vector2 center, Vector2 size) {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Position of a rect's centre expressed in the local space of `layer` (a centre-anchored full-screen rect).
    public static Vector2 LocalCenter(RectTransform layer, RectTransform target) {
        return layer.InverseTransformPoint(target.TransformPoint(target.rect.center));
    }

    // ---------- particle burst ----------
    sealed class Particle {
        public RectTransform rect; public Image image; public Vector2 pos, vel; public float life, age, spin, size;
    }

    public static void Burst(MonoBehaviour host, RectTransform layer, Vector2 center, int count, Sprite sprite, Color from, Color to,
        float speed, float life, float size, float gravity = 0f, float angle = 90f, float spread = 360f, float drag = 1.6f) {
        host.StartCoroutine(BurstRoutine(layer, center, count, sprite, from, to, speed, life, size, gravity, angle, spread, drag));
    }

    static IEnumerator BurstRoutine(RectTransform layer, Vector2 center, int count, Sprite sprite, Color from, Color to,
        float speed, float life, float size, float gravity, float angle, float spread, float drag) {
        var list = new List<Particle>(count);
        for (int i = 0; i < count; i++) {
            float a = (angle + UnityEngine.Random.Range(-spread * .5f, spread * .5f)) * Mathf.Deg2Rad;
            float s = speed * UnityEngine.Random.Range(.35f, 1f);
            var image = Add(layer, "fx", sprite, from, center, Vector2.one * size);
            list.Add(new Particle {
                rect = image.rectTransform, image = image, pos = center, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s,
                life = life * UnityEngine.Random.Range(.7f, 1.15f), spin = UnityEngine.Random.Range(-240f, 240f), size = size * UnityEngine.Random.Range(.6f, 1.15f)
            });
        }
        bool alive = true;
        while (alive) {
            alive = false;
            float dt = Time.unscaledDeltaTime;
            foreach (var p in list) {
                if (p.rect == null) continue;
                p.age += dt;
                float u = p.age / p.life;
                if (u >= 1f) { UnityEngine.Object.Destroy(p.rect.gameObject); p.rect = null; continue; }
                alive = true;
                p.vel *= Mathf.Max(0f, 1f - drag * dt);
                p.vel.y -= gravity * dt;
                p.pos += p.vel * dt;
                p.rect.anchoredPosition = p.pos;
                p.rect.localRotation = Quaternion.Euler(0, 0, p.spin * p.age);
                float pop = Mathf.Min(1f, p.age / .08f);
                p.rect.sizeDelta = Vector2.one * p.size * pop * Mathf.Lerp(1f, .25f, u);
                var c = Color.Lerp(from, to, u);
                c.a = Mathf.Lerp(from.a, 0f, InQuad(u));
                p.image.color = c;
            }
            yield return null;
        }
    }

    // Expanding ring / flash that fades out.
    public static void Pulse(MonoBehaviour host, RectTransform layer, Sprite sprite, Vector2 center, Color color, float fromSize, float toSize, float duration, float delay = 0f) {
        var image = Add(layer, "pulse", sprite, new Color(color.r, color.g, color.b, 0), center, Vector2.one * fromSize);
        Tween(host, duration, u => {
            if (image == null) return;
            float e = OutCubic(u);
            image.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(fromSize, toSize, e);
            var c = color; c.a = color.a * (1f - u) * Mathf.Min(1f, u * 12f);
            image.color = c;
        }, () => { if (image != null) UnityEngine.Object.Destroy(image.gameObject); }, delay);
    }

    // Icons flying along a curve (coins, sparks). `arrive` runs once per icon.
    public static void FlyIcons(MonoBehaviour host, RectTransform layer, Vector2 from, Vector2 to, Sprite sprite, int count, float size,
        float duration, Action arrive = null, Action all = null, float stagger = .06f, Color? tint = null) {
        int left = count;
        for (int i = 0; i < count; i++) {
            var image = Add(layer, "fly", sprite, tint ?? Color.white, from, Vector2.zero);
            Vector2 mid = (from + to) * .5f + new Vector2(UnityEngine.Random.Range(-140f, 140f), UnityEngine.Random.Range(90f, 230f));
            float spin = UnityEngine.Random.Range(-360f, 360f);
            float dur = duration * UnityEngine.Random.Range(.9f, 1.1f);
            Tween(host, dur, u => {
                if (image == null) return;
                float e = Mathf.Pow(u, 1.5f);
                Vector2 p = Mathf.Pow(1 - e, 2) * from + 2 * (1 - e) * e * mid + e * e * to;
                image.rectTransform.anchoredPosition = p;
                image.rectTransform.sizeDelta = Vector2.one * size * Mathf.Min(1f, u * 8f) * Mathf.Lerp(1f, .55f, u);
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, spin * u);
            }, () => {
                if (image != null) UnityEngine.Object.Destroy(image.gameObject);
                if (arrive != null) arrive();
                if (--left == 0 && all != null) all();
            }, i * stagger);
        }
    }

    public static void FloatText(MonoBehaviour host, RectTransform layer, Font font, string text, Vector2 at, Color color, int size = 30, float rise = 80f, float life = 1.05f) {
        var go = new GameObject("float text", typeof(RectTransform), typeof(Text), typeof(Outline));
        var rect = (RectTransform)go.transform;
        rect.SetParent(layer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(420, 60);
        rect.anchoredPosition = at;
        var label = go.GetComponent<Text>();
        label.text = text; label.font = font; label.fontSize = size; label.color = color;
        label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(.15f, .06f, 0, .9f); outline.effectDistance = new Vector2(2f, -2f);
        Tween(host, life, u => {
            if (rect == null) return;
            rect.anchoredPosition = at + new Vector2(0, rise * OutCubic(u));
            rect.localScale = Vector3.one * Mathf.Lerp(.4f, 1f, OutBack(Mathf.Min(1f, u * 4.5f)));
            var c = color; c.a = u < .6f ? 1f : 1f - (u - .6f) / .4f;
            label.color = c;
        }, () => { if (go != null) UnityEngine.Object.Destroy(go); });
    }
}

// Spring-based press feedback: squash on press, overshoot on release, optional hover grow.
public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler {
    public float pressedScale = .9f, hoverScale = 1.04f, restScale = 1f;
    public float stiffness = 420f, damping = 19f;
    float scale = 1f, velocity;
    bool down, hover;
    Vector3 baseScale = Vector3.one;

    void Awake() { baseScale = transform.localScale; }
    public void Punch(float impulse) { velocity += impulse; }
    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e) { down = false; velocity += 2.2f; }
    public void OnPointerEnter(PointerEventData e) { if (e.pointerId < 0 || e.pointerId == -1) hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
    void OnDisable() { scale = restScale; velocity = 0; down = hover = false; transform.localScale = baseScale * restScale; }

    void Update() {
        float target = down ? pressedScale : (hover ? hoverScale * restScale : restScale);
        float dt = Mathf.Min(Time.unscaledDeltaTime, .033f);
        if (Mathf.Abs(target - scale) < .0004f && Mathf.Abs(velocity) < .002f) { if (scale != target) { scale = target; transform.localScale = baseScale * scale; } return; }
        velocity += ((target - scale) * stiffness - velocity * damping) * dt;
        scale += velocity * dt;
        transform.localScale = baseScale * scale;
    }
}
