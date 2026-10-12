using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class BattleParallax : MonoBehaviour {
    [Range(0, 1)] public float followStrength = .16f;
    [Range(0, 2)] public float maxTravel = .85f;
    public float smoothing = .28f;
    public float FarSpeed = .06f, MiddleSpeed = .3f, ForegroundSpeed = 1.25f;
    BattleStage stage;
    Transform subject;
    Camera view;
    Vector3 cameraHome, farHome, middleHome, frontHome;
    float subjectHome, velocity;
    public void Bind(Camera camera, BattleStage battlefield, Transform actor) {
        view = camera; stage = battlefield; subject = actor;
        cameraHome = view.transform.position; subjectHome = actor.position.x;
        farHome = stage.far.localPosition; middleHome = stage.middle.localPosition; frontHome = stage.foreground.localPosition;
    }
    void LateUpdate() {
        if (view == null || subject == null || stage == null) return;
        float desired = cameraHome.x + Mathf.Clamp((subject.position.x - subjectHome) * followStrength, -maxTravel, maxTravel);
        float x = view.transform.position.x;
        if (Time.deltaTime > 0) x = Mathf.SmoothDamp(x, desired, ref velocity, smoothing, Mathf.Infinity, Time.deltaTime);
        view.transform.position = new Vector3(x, cameraHome.y, cameraHome.z);
        ApplyOffset(x - cameraHome.x);
        view.orthographicSize = Mathf.Max(BattleStage.CameraHeight, 10.3f / view.aspect);
        stage.FrameForeground(view.orthographicSize * view.aspect, view.orthographicSize);
    }
    public void ApplyOffset(float cameraOffset) {
        // Near group remains fixed in world space, including actors, props and contact shadows.
        stage.far.localPosition = farHome + Vector3.right * cameraOffset * (1 - FarSpeed);
        stage.middle.localPosition = middleHome + Vector3.right * cameraOffset * (1 - MiddleSpeed);
        stage.foreground.localPosition = frontHome + Vector3.right * cameraOffset * (1 - ForegroundSpeed);
    }
    void OnDisable() {
        if (view != null) view.transform.position = cameraHome;
        if (stage != null && stage.far != null) ApplyOffset(0);
    }
}
