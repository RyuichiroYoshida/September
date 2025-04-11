namespace September.OgerSystem
{
    public class IDGenerator
    {
        private static int _currentID = 0;

        public static string GenerateID()
        {
            _currentID++;
            return $"player{_currentID:D4}";
        }
    }
}

