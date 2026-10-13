using Spine;
using Spine.Unity;
using UnityEngine;

// These training actions are ground actions. Anchor the visible boots, not weapon/FX bounds.
public sealed class BattleGrounding : MonoBehaviour {
    const float SoleInset = .012f;
    SkeletonAnimation hero;
    Transform foot;
    Slot front, back;
    SpriteRenderer ambient, frontContact, backContact;
    float[] vertices = new float[32];

    public void Bind(SkeletonAnimation animation, Transform anchor) {
        hero = animation; foot = anchor;
        front = hero.Skeleton.FindSlot("armor_footF");
        back = hero.Skeleton.FindSlot("armor_footB");
        if (!TryGetBootBounds(front, out var a) || !TryGetBootBounds(back, out var b)) return;
        var position = transform.localPosition;
        position.x = -(a.center.x + b.center.x) * .5f * transform.localScale.x;
        transform.localPosition = position;
        ambient = foot.Find("Contact shadow")?.GetComponent<SpriteRenderer>();
        // Portraits use the same foot alignment without spawning ground artwork.
        if (ambient != null) {
            frontContact = MakeContact("Front boot contact");
            backContact = MakeContact("Back boot contact");
        }
        hero.UpdateComplete += UpdateGrounding;
        UpdateGrounding(hero);
    }

    SpriteRenderer MakeContact(string title) {
        var contact = BattleStage.Anchor(foot, title, Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
        contact.sprite = ambient.sprite; contact.sharedMaterial = ambient.sharedMaterial;
        contact.sortingOrder = -19;
        return contact;
    }

    void UpdateGrounding(ISkeletonAnimation animation) {
        if (!TryGetBootBounds(front, out var a) || !TryGetBootBounds(back, out var b)) return;
        float scale = transform.localScale.y;
        var position = transform.localPosition;
        position.y = -Mathf.Min(a.min.y, b.min.y) * scale - SoleInset;
        transform.localPosition = position;
        if (ambient == null) return;
        Vector3 left = transform.localPosition + Vector3.Scale(new Vector3(a.center.x, a.min.y, 0), transform.localScale);
        Vector3 right = transform.localPosition + Vector3.Scale(new Vector3(b.center.x, b.min.y, 0), transform.localScale);
        float span = Mathf.Abs(left.x - right.x);
        ambient.transform.localPosition = new Vector3((left.x + right.x) * .5f, .015f, 0);
        SetSize(ambient, Mathf.Clamp(span + .48f, .7f, 1.05f), .23f);
        UpdateContact(frontContact, left, a.size.x * Mathf.Abs(transform.localScale.x), ambient.color);
        UpdateContact(backContact, right, b.size.x * Mathf.Abs(transform.localScale.x), ambient.color);
    }

    static void UpdateContact(SpriteRenderer contact, Vector3 sole, float bootWidth, Color tint) {
        float lift = Mathf.Max(0, sole.y);
        // A raised boot loses its dark contact patch while the planted boot stays attached.
        contact.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(1, .12f, Mathf.Clamp01(lift / .18f)));
        contact.transform.localPosition = new Vector3(sole.x, .008f, 0);
        SetSize(contact, Mathf.Clamp(bootWidth * 1.35f, .2f, .4f), .09f);
    }

    static void SetSize(SpriteRenderer renderer, float width, float depth) {
        renderer.transform.localScale = new Vector3(width / renderer.sprite.bounds.size.x, depth / renderer.sprite.bounds.size.y, 1);
    }

    // Attachment vertices include skinned/weighted boot meshes and mirrored poses.
    public bool TryGetBootBounds(Slot slot, out Bounds bounds) {
        bounds = default;
        if (slot == null || !slot.Bone.Active) return false;
        int count;
        if (slot.Attachment is RegionAttachment region) {
            count = 8; region.ComputeWorldVertices(slot, vertices, 0);
        } else if (slot.Attachment is MeshAttachment mesh) {
            count = mesh.WorldVerticesLength;
            if (vertices.Length < count) vertices = new float[count];
            mesh.ComputeWorldVertices(slot, 0, count, vertices, 0);
        } else return false;
        if (count < 2) return false;
        bounds = new Bounds(new Vector3(vertices[0], vertices[1], 0), Vector3.zero);
        for (int i = 2; i < count; i += 2) bounds.Encapsulate(new Vector3(vertices[i], vertices[i + 1], 0));
        return true;
    }

    void OnDestroy() { if (hero != null) hero.UpdateComplete -= UpdateGrounding; }
}
