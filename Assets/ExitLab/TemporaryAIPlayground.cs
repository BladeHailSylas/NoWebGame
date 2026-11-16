using System.Collections.Generic;
using Characters;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Stacks.Definition;
using Systems.Time;
using UnityEngine;

namespace ExitLab
{
    /// <summary>빈 검증 Scene에서만 사용합니다. 모든 설정은 메모리에 생성하며 기존 SkillData를 저장하지 않습니다.</summary>
    public sealed class TemporaryAIPlayground : MonoBehaviour
    {
        private EnemyEntity _enemy;
        private TemporaryAITarget _you, _ally, _friend, _neutral;
        private SkillData _skill;
        private CharacterSpec _spec;
        private Texture2D _texture;
        private Sprite _sprite;
        private bool _targetsNear = true;

        private void Start()
        {
            if (FindFirstObjectByType<TickerRunner>() || FindFirstObjectByType<CommandCollector>())
            { Debug.LogError("TemporaryAIPlayground는 다른 Ticker/Collector가 없는 빈 Scene에서 실행하세요."); enabled = false; return; }
            var you = LayerMask.NameToLayer("You");
            var foe = LayerMask.NameToLayer("Foe");
            var ally = LayerMask.NameToLayer("Ally");
            if (you < 0 || foe < 0 || ally < 0)
            { Debug.LogError("You/Foe/Ally Layer가 필요합니다."); enabled = false; return; }
            _texture = new Texture2D(1, 1);
            _texture.SetPixel(0, 0, Color.white); _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            var time = Child("AI test ticker");
            time.AddComponent<TickerRunner>(); // 먼저 서비스 준비
            var services = Child("AI test commands");
            services.AddComponent<TargetResolver>();
            var collector = services.AddComponent<CommandCollector>();

            // 직접 대상 공격으로 입력 연결을 관찰합니다. 기존 LayerMask→관계 Mask 변환을 흉내 내지 않습니다.
            _skill = ScriptableObject.CreateInstance<SkillData>();
            var damage = JsonUtility.FromJson<DamageMechanism>(
                "{\"minRange\":250,\"maxRange\":3000,\"delayTicks\":2,\"damageValue\":100}");
            _skill.mechanismData = new MechanismData { mechanism = damage, mode = TargetMode.TowardsEntity };
            _spec = ScriptableObject.CreateInstance<CharacterSpec>();
            _spec.baseHp = 10000; _spec.baseAttack = 100; _spec.baseSpeed = 1500;
            _spec.CharacterVariables = new List<VariableDefinition>();
            _spec.attack = new SkillBinding { slot = SkillSlot.Attack, mode = TargetMode.TowardsEntity, skillData = _skill };

            var enemyObject = Child("AI Caster (Foe)");
            enemyObject.SetActive(false); enemyObject.layer = foe;
            var body = enemyObject.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
            var collider = enemyObject.AddComponent<CircleCollider2D>(); collider.radius = .2f;
            _enemy = enemyObject.AddComponent<EnemyEntity>();
            _enemy.InstallSpec(_spec); _enemy.ConfigureTemporaryAI(collector, log: true);
            Visual(enemyObject, Color.red);
            enemyObject.SetActive(true);
            _you = Target("You target", you, new Vector2(1.5f, 0), Color.cyan);
            _ally = Target("Ally target", ally, new Vector2(2.5f, 1), Color.blue);
            _friend = Target("Foe friend", foe, new Vector2(-1.5f, 0), Color.yellow);
            _neutral = Target("Explicit Neutral", 0, new Vector2(0, 1.5f), Color.green);
            _neutral.RelationProfile = EntityRelationProfile.ExplicitNeutral;
            if (!Camera.main)
            {
                var camera = Child("AI test camera").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6;
                camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.12f, .12f, .15f);
                camera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private GameObject Child(string objectName)
        {
            var go = new GameObject(objectName); go.transform.SetParent(transform); return go;
        }
        private void Visual(GameObject go, Color color)
        {
            // 표시 크기를 바꾸더라도 Entity/Collider 스케일은 변경하지 않습니다.
            var visual = new GameObject("visual"); visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * .4f;
            var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = _sprite; renderer.color = color;
        }
        private TemporaryAITarget Target(string objectName, int layer, Vector2 position, Color color)
        {
            var go = Child(objectName); go.layer = layer; go.transform.position = position;
            var target = go.AddComponent<TemporaryAITarget>();
            var child = new GameObject("Default layer child collider"); child.transform.SetParent(go.transform, false);
            child.AddComponent<CircleCollider2D>().radius = .25f;
            Visual(go, color);
            return target;
        }

        private void OnGUI()
        {
            if (!_enemy) return;
            GUILayout.BeginArea(new Rect(12, 12, 450, 310), GUI.skin.box);
            GUILayout.Label("Temporary AI — 60 ticks / direct Attack fixture");
            GUILayout.Label($"Decisions: {_enemy.AIDecisionCount}, Target: {(_enemy.AITarget ? _enemy.AITarget.name : "none")}");
            GUILayout.Label($"Hits: You={_you.HitCount}, Ally={_ally.HitCount}, Foe={_friend.HitCount}, Neutral={_neutral.HitCount}");
            if (GUILayout.Button(_enemy.TemporaryAIEnabled ? "Disable AI" : "Enable AI"))
                _enemy.TemporaryAIEnabled = !_enemy.TemporaryAIEnabled;
            if (GUILayout.Button("Move all targets far / return near"))
            {
                _targetsNear = !_targetsNear;
                var origin = (Vector2)_enemy.transform.position;
                _you.transform.position = origin + new Vector2(_targetsNear ? 1.5f : 100, 0);
                _ally.transform.position = origin + new Vector2(_targetsNear ? 2.5f : 101, 1);
                _friend.transform.position = origin + new Vector2(_targetsNear ? -1.5f : 102, 0);
                _neutral.transform.position = origin + new Vector2(0, _targetsNear ? 1.5f : 100);
            }
            if (GUILayout.Button("Toggle caster Foe / You"))
            {
                _enemy.gameObject.layer = _enemy.gameObject.layer == LayerMask.NameToLayer("Foe")
                    ? LayerMask.NameToLayer("You") : LayerMask.NameToLayer("Foe");
            }
            if (GUILayout.Button("Toggle You targetable")) _you.targetable = !_you.targetable;
            GUILayout.Label("Red: caster / Cyan: You / Blue: Ally / Yellow: Foe / Green: Neutral");
            GUILayout.Label("Selection + explicit Attack only; projectile/area mask conversion is pending.");
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (_sprite) Destroy(_sprite); if (_texture) Destroy(_texture);
            if (_skill) Destroy(_skill); if (_spec) Destroy(_spec);
        }
    }
}
