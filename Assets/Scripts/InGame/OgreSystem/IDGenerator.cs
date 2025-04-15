namespace September.OgreSystem
{
    public class IDGenerator
    {
        private static int _currentID = 0;

        public static int GenerateID()
        {
            _currentID++;
            return _currentID;
        }
    }
}

