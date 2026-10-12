using UnityEngine;

// Small mesh-based effects: no sprite-sheet timing dependency and no texture downscaling.
public sealed class BattleEffect : MonoBehaviour {
    public float lifetime = .3f;
    public Vector3 velocity;
    public float angularVelocity;
    public float expansion;
    float age;
    Vector3 originalScale;
    Color tint;
    Material ownMaterial;
    Mesh ownMesh;
    public static BattleEffect Make(string shape, Vector3 position, Color color, float size, float duration, int order = 111) {
        var obj = new GameObject("FX " + shape);
        obj.transform.position = position; obj.transform.localScale = Vector3.one * size;
        var filter = obj.AddComponent<MeshFilter>(); var renderer = obj.AddComponent<MeshRenderer>();
        var template = Resources.Load<Material>("Battle/SpriteMaterial");
        var material = template != null ? new Material(template) : new Material(Shader.Find("Sprites/Default")); material.color = color;
        renderer.sharedMaterial = material; renderer.sortingOrder = order;
        var mesh = new Mesh();
        if (shape == "slash" || shape == "ring") {
            const int n = 28;
            var vertices = new Vector3[(n + 1) * 2]; var triangles = new int[n * 6];
            for (int i = 0; i <= n; i++) {
                float t = i / (float)n;
                float a = shape == "ring" ? t * Mathf.PI * 2 : Mathf.Lerp(-1.1f, 1.5f, t);
                float thickness = shape == "ring" ? .09f : Mathf.Sin(t * Mathf.PI) * .24f;
                vertices[i*2] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                vertices[i*2+1] = vertices[i*2] * (1 - thickness);
                if (i < n) { int v = i*2, k=i*6; triangles[k]=v; triangles[k+1]=v+2; triangles[k+2]=v+1; triangles[k+3]=v+1; triangles[k+4]=v+2; triangles[k+5]=v+3; }
            }
            mesh.vertices=vertices; mesh.triangles=triangles;
        } else if (shape == "arrow") {
            mesh.vertices = new[] { new Vector3(-.8f,-.035f),new Vector3(.45f,-.035f),new Vector3(.45f,-.16f),new Vector3(.85f,0),new Vector3(.45f,.16f),new Vector3(.45f,.035f),new Vector3(-.8f,.035f) };
            mesh.triangles = new[] {0,1,6,1,5,6,2,3,4};
        } else {
            const int n=16; var v=new Vector3[n+1]; var t=new int[n*3];
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n; v[i+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a)); t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=(i+1)%n+1;}
            mesh.vertices=v;mesh.triangles=t;
        }
        var colors=new Color[mesh.vertexCount]; for(int i=0;i<colors.Length;i++) colors[i]=Color.white; mesh.colors=colors;
        mesh.RecalculateBounds(); filter.sharedMesh=mesh;
        var effect=obj.AddComponent<BattleEffect>(); effect.ownMaterial=material; effect.ownMesh=mesh;
        effect.tint=color;effect.lifetime=duration;effect.originalScale=obj.transform.localScale;
        return effect;
    }
    void Update() {
        age += Time.deltaTime;
        float t=Mathf.Clamp01(age/lifetime);
        transform.position += velocity*Time.deltaTime;
        transform.Rotate(0,0,angularVelocity*Time.deltaTime);
        transform.localScale=originalScale*(1+expansion*t);
        ownMaterial.color=new Color(tint.r,tint.g,tint.b,tint.a*(1-t));
        if(age>=lifetime) Destroy(gameObject);
    }
    void OnDestroy(){if(ownMaterial!=null)Destroy(ownMaterial);if(ownMesh!=null)Destroy(ownMesh);}
}


