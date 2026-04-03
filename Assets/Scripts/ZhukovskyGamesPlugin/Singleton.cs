namespace ZhukovskyGamesPlugin {
    public class Singleton<T> : SingletonBase<T> where T : CustomMonoBehaviour {
        private void Awake() {
            CreateSingleton();
        }
    }
}