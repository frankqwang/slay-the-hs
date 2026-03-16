public sealed class EnemyUnit
{
    public string Name = "Enemy";
    public string VisualId = "cultist";
    public int Hp;
    public int MaxHp;
    public int Block;
    public int Strength;
    public int Vulnerable;
    public EnemyIntentType IntentType;
    public int IntentValue;
    public bool IsAlive => Hp > 0;
}
