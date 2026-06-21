public class Skill 
{
    string name;
    string description;
    public float Power;
    
    public int Roll(Character _character)
    {
        return _character.Resolver.Roll();
    }
}