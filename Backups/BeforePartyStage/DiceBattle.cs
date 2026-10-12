using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DiceBattle : MonoBehaviour {
    public SkeletonDataAsset dataAsset;
    public BattleStage stage;
    enum Phase { NeedRoll, Rolling, Ready, Attacking, Reward }
    Phase phase;
    BattleHud hud;
    SkeletonAnimation hero;
    Camera battleCamera;
    AudioSource sound;
    readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    readonly int[] values=new int[5];
    readonly bool[] held=new bool[5];
    readonly int[] professions={0,4,6};
    readonly string[] titles={"战士","游侠","法师"};
    int role,round=1,wave=1,health=300,maxHealth=300,rollsLeft=3,maxRolls=3,baseDamage=14;
    float bonusMultiplier;
    bool paused,muted;
    float previousTimeScale=1;
    Vector3 home;
    Coroutine recoil;
    HeroProfession Profession { get { return HeroProfession.All[professions[role]]; } }
    void Start() {
        if(dataAsset==null||dataAsset.GetSkeletonData(false)==null){Debug.LogError("Dice Training: HeroSkeleton is missing or failed to load.",this);enabled=false;return;}
        battleCamera=Camera.main;
        if(battleCamera==null){battleCamera=new GameObject("Battle Camera").AddComponent<Camera>();battleCamera.tag="MainCamera";}
        battleCamera.orthographic=true;battleCamera.orthographicSize=3.2f;battleCamera.transform.position=new Vector3(0,0,-10);
        battleCamera.rect=new Rect(0,.335f,1,.53f);battleCamera.backgroundColor=new Color(.15f,.21f,.23f);
        if(FindObjectOfType<AudioListener>()==null)battleCamera.gameObject.AddComponent<AudioListener>();
        sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=.65f;
        foreach(string name in new[]{"dice-roll","dice-lock","sword-swing","wood-impact","bow-release","arcane-cast","victory","ui-click"})clips[name]=Resources.Load<AudioClip>("Battle/Audio/"+name);
        if(stage==null){var obj=new GameObject("Layered training ground");obj.transform.SetParent(transform,false);stage=obj.AddComponent<BattleStage>();}
        stage.Build();home=stage.heroPoint.position;
        hero=SkeletonAnimation.NewSkeletonAnimationGameObject(dataAsset);hero.name="Class hero";hero.transform.SetParent(stage.heroPoint,false);hero.transform.localScale=Vector3.one*1.5f;hero.Initialize(false);stage.heroPoint.gameObject.AddComponent<BattleDepth>();
        if(EventSystem.current==null){var events=new GameObject("Battle input",typeof(EventSystem),typeof(StandaloneInputModule));events.transform.SetParent(transform,false);}
        var ui=new GameObject("Battle HUD");ui.transform.SetParent(transform,false);hud=ui.AddComponent<BattleHud>();hud.Build(this);
        ApplyProfession();Refresh();
    }
    void ApplyProfession() {
        var data=hero.Skeleton.Data;
        var selected=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var skin in data.Skins.OrderBy(s=>s.Name,StringComparer.Ordinal)) {
            if(!skin.Name.StartsWith("001_appearance/",StringComparison.Ordinal))continue;
            string key=skin.Name.Substring(0,skin.Name.LastIndexOf('/'));
            if(!selected.ContainsKey(key))selected[key]=skin.Name;
        }
        selected["001_appearance/head"]="001_appearance/head/head_001";
        selected["001_appearance/dress/headwear"]="001_appearance/dress/headwear/headwear_000";
        selected["001_appearance/dress/Tail"]="001_appearance/dress/Tail/Tail_000";
        selected["001_appearance/head_hair"]="001_appearance/head_hair/"+Profession.hair;
        selected["001_appearance/dress/backpack"]="001_appearance/dress/backpack/"+Profession.backpack;
        selected["001_appearance/dress/eyewear"]="001_appearance/dress/eyewear/"+Profession.eyewear;
        selected["002A_armor"]="002A_armor/"+Profession.armor;
        selected["003_weapon"]="003_weapon/"+Profession.weapon;
        var combined=new Skin("battle-"+Profession.id);
        if(data.DefaultSkin!=null)combined.AddSkin(data.DefaultSkin);
        foreach(var name in selected.Values){var skin=data.FindSkin(name);if(skin!=null)combined.AddSkin(skin);else Debug.LogWarning("Battle outfit skin missing: "+name,this);}
        hero.AnimationState.ClearTracks();hero.Skeleton.SetSkin(combined);hero.Skeleton.SetToSetupPose();hero.Skeleton.ScaleX=-1;Play("001A_idle",true);hero.AnimationState.Apply(hero.Skeleton);
        hero.Skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
        float[] vertices=null;float x,y,width,height;hero.Skeleton.GetBounds(out x,out y,out width,out height,ref vertices);
        if(height>.01f && !float.IsInfinity(height)){float scale=Mathf.Clamp(2.4f/height,.3f,2f);hero.transform.localScale=Vector3.one*scale;hero.transform.localPosition=new Vector3(0,-y*scale,0);}
    }
    float Play(string token,bool loop) {
        var animation=hero.Skeleton.Data.Animations.FirstOrDefault(a=>a.Name.StartsWith(Profession.animationGroup+"/",StringComparison.Ordinal)&&a.Name.Substring(Profession.animationGroup.Length+1).StartsWith(token,StringComparison.Ordinal)&&a.Name.IndexOf("backup",StringComparison.OrdinalIgnoreCase)<0);
        if(animation==null&&token!="001A_idle")return Play("001A_idle",true);
        if(animation==null)return .7f;
        hero.AnimationState.SetAnimation(0,animation,loop);return animation.Duration;
    }
    public void SelectRole(int index) {
        if(hero==null||paused||index<0||index>2||phase==Phase.Rolling||phase==Phase.Attacking||phase==Phase.Reward)return;
        role=index;ApplyProfession();ResetDice();Sound("ui-click");Refresh();
    }
    public void ToggleHold(int index) {
        if(paused||phase!=Phase.Ready||index<0||index>=5)return;
        held[index]=!held[index];Sound("dice-lock");Refresh();
    }
    public void RollDice(){if(paused||(phase!=Phase.NeedRoll&&phase!=Phase.Ready)||rollsLeft<=0||held.All(x=>x))return;StartCoroutine(RollRoutine());}
    IEnumerator RollRoutine() {
        phase=Phase.Rolling;rollsLeft--;Sound("dice-roll");Refresh();
        for(int step=0;step<9;step++){for(int i=0;i<5;i++)if(!held[i])values[i]=UnityEngine.Random.Range(1,7);Refresh();yield return new WaitForSeconds(.055f);}
        phase=Phase.Ready;Refresh();
    }
    int Damage(){var combo=BattleDiceRules.Evaluate(values);return Mathf.RoundToInt((baseDamage+combo.Sum*2)*(combo.Multiplier+bonusMultiplier));}
    public void Attack(){if(paused||phase!=Phase.Ready)return;StartCoroutine(AttackRoutine());}
    IEnumerator AttackRoutine() {
        phase=Phase.Attacking;Refresh();int damage=Damage();var combo=BattleDiceRules.Evaluate(values);
        Vector3 target=stage.dummyPoint.position+Vector3.up*1.3f;
        if(role==0) {
            Play("002A_move",true);yield return MoveHero(home,stage.dummyPoint.position+Vector3.left*1.75f,.3f);
            float duration=Play(combo.Multiplier>=2?"004A":"003A",false);Sound("sword-swing");yield return new WaitForSeconds(Mathf.Clamp(duration*.42f,.15f,.65f));
            BattleEffect.Make("slash",target,new Color(1,.83f,.38f),1.2f,.3f);Hit(damage,target);
            yield return new WaitForSeconds(.3f);Play("002A_move",true);yield return MoveHero(stage.heroPoint.position,home,.32f);
        } else {
            float duration=Play(combo.Multiplier>=2?"004A":"003A",false);Sound(role==1?"bow-release":"arcane-cast");yield return new WaitForSeconds(Mathf.Clamp(duration*.4f,.15f,.65f));
            int hits=role==1?3:1;
            for(int hit=0;hit<hits;hit++) {
                Vector3 origin=stage.heroPoint.position+new Vector3(.8f,1.3f,0);
                var projectile=BattleEffect.Make(role==1?"arrow":"orb",origin,role==1?new Color(1,.86f,.5f):new Color(.35f,.85f,1),role==1?.65f:.22f,.38f);
                projectile.velocity=(target-origin)/.3f;
                yield return new WaitForSeconds(.3f);
                if(projectile!=null)Destroy(projectile.gameObject);
                if(role==2){var ring=BattleEffect.Make("ring",target,new Color(.35f,.85f,1),.6f,.4f);ring.expansion=3;}
                Hit(damage/hits+(hit<damage%hits?1:0),target);yield return new WaitForSeconds(.08f);
            }
        }
        Play("001A_idle",true);yield return new WaitForSeconds(.25f);
        if(health<=0){phase=Phase.Reward;hud.RewardTitle.text="木桩击破 · 选择一份契约";hud.RewardChoices[1].interactable=maxRolls<5;BattleHud.SetButton(hud.RewardChoices[1],maxRolls<5?"命运契约\n每轮掷骰次数 +1\n（最多 5 次）":"命运契约\n已达到 5 次上限");hud.Rewards.SetActive(true);Sound("victory");}
        else {round++;ResetDice();}
        Refresh();
    }
    IEnumerator MoveHero(Vector3 from,Vector3 to,float duration){float t=0;while(t<duration){t+=Time.deltaTime;stage.heroPoint.position=Vector3.Lerp(from,to,Mathf.SmoothStep(0,1,t/duration));yield return null;}stage.heroPoint.position=to;}
    void Hit(int damage,Vector3 point) {
        health=Mathf.Max(0,health-damage);Sound("wood-impact");
        for(int i=0;i<8;i++){var spark=BattleEffect.Make("spark",point,new Color(1,.76f,.35f),UnityEngine.Random.Range(.025f,.055f),.28f);spark.velocity=UnityEngine.Random.insideUnitCircle*3.5f;}
        if(recoil!=null)StopCoroutine(recoil);recoil=StartCoroutine(Recoil());
        StartCoroutine(FloatDamage(hud.FloatingText(damage.ToString(),point+Vector3.up*.6f,battleCamera,new Color(1,.88f,.5f))));Refresh();
    }
    IEnumerator Recoil(){float t=0;while(t<.25f){t+=Time.deltaTime;stage.dummy.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*65)*7*(1-t/.25f));stage.dummy.color=Color.Lerp(new Color(1,.45f,.25f),Color.white,t/.25f);yield return null;}stage.dummy.transform.localRotation=Quaternion.identity;stage.dummy.color=Color.white;recoil=null;}
    IEnumerator FloatDamage(Text text){float t=0;while(t<.8f){t+=Time.deltaTime;if(text==null)yield break;text.rectTransform.anchoredPosition+=Vector2.up*(Time.deltaTime*85);var color=text.color;color.a=1-t/.8f;text.color=color;yield return null;}Destroy(text.gameObject);}
    public void ChooseReward(int choice) {
        if(paused||phase!=Phase.Reward||choice<0||choice>2)return;
        if(choice==0)baseDamage+=8;else if(choice==1){if(maxRolls>=5)return;maxRolls++;}else bonusMultiplier+=.2f;
        wave++;round++;maxHealth=300+(wave-1)*100;health=maxHealth;hud.Rewards.SetActive(false);ResetDice();Sound("ui-click");Refresh();
    }
    void ResetDice(){Array.Clear(values,0,5);Array.Clear(held,0,5);rollsLeft=maxRolls;phase=Phase.NeedRoll;}
    public void ToggleAudio(){muted=!muted;if(sound!=null)sound.mute=muted;Refresh();}
    public void TogglePause(){paused=!paused;if(paused){previousTimeScale=Time.timeScale;Time.timeScale=0;}else Time.timeScale=previousTimeScale;hud.PauseOverlay.SetActive(paused);Refresh();}
    void OnApplicationPause(bool value){if(value&&hud!=null&&!paused)TogglePause();}
    void OnDestroy(){if(paused)Time.timeScale=previousTimeScale;}
    void Sound(string key){if(sound!=null&&clips.TryGetValue(key,out var clip)&&clip!=null)sound.PlayOneShot(clip);}
    void Refresh() {
        if(hud==null)return;
        bool ready=phase==Phase.Ready;
        hud.Round.text="第 "+wave+" 个木桩  /  第 "+round+" 回合";
        hud.Career.text=titles[role]+" · "+(role==0?"近身斩击":role==1?"三连箭":"奥术冲击");
        hud.Target.text="训练木桩  "+health+" / "+maxHealth;
        hud.Health.rectTransform.anchorMax=new Vector2(health/(float)maxHealth,1);
        hud.AudioLabel.text=muted?"声音：关":"声音：开";hud.PauseLabel.text=paused?"继续":"暂停";
        hud.Hint.text=phase==Phase.NeedRoll?"掷骰 → 点击保留 → 攻击":phase==Phase.Rolling?"骰子转动中…":phase==Phase.Attacking?"正在攻击木桩…":phase==Phase.Reward?"选择契约，继续训练":"点骰子保留，再掷或直接攻击";
        hud.Damage.text=ready?"预计伤害  "+Damage():"点数决定伤害";
        if(ready){var combo=BattleDiceRules.Evaluate(values);hud.Combo.text=combo.Name+"   ×"+(combo.Multiplier+bonusMultiplier).ToString("0.0")+"   /   点数 "+combo.Sum;}else hud.Combo.text=phase==Phase.Rolling?"寻找更强的组合":"五枚骰子 · 一次决策";
        BattleHud.SetButton(hud.Roll,"掷骰  "+rollsLeft+" / "+maxRolls);hud.Roll.interactable=!paused&&(ready||phase==Phase.NeedRoll)&&rollsLeft>0&&!held.All(x=>x);
        hud.Attack.interactable=!paused&&ready;
        for(int i=0;i<5;i++){hud.Faces[i].Show(values[i]);hud.Unknown[i].gameObject.SetActive(values[i]==0);hud.Dice[i].interactable=!paused&&ready;hud.Dice[i].image.color=held[i]?new Color(1,.73f,.32f):new Color(.9f,.91f,.84f);hud.Holds[i].text=held[i]?"已保留":"点击保留";}
        for(int i=0;i<3;i++){hud.Roles[i].interactable=!paused&&(ready||phase==Phase.NeedRoll);hud.Roles[i].image.color=i==role?new Color(.6f,.42f,.18f):new Color(.18f,.24f,.25f);}
    }
}


