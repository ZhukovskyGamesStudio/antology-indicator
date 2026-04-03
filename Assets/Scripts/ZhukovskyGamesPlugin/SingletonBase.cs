namespace ZhukovskyGamesPlugin {
    public abstract class SingletonBase<T> : CustomMonoBehaviour where T : CustomMonoBehaviour {
        public static T Instance { get; private set; }
        protected virtual bool IsDontDestroyOnLoad => true;

        protected void CreateSingleton() {
            if (Instance == null) {
                Instance = this as T;
                OnFirstInit();
                if (IsDontDestroyOnLoad) {
                    transform.SetParent(DdolContainer.Container);
                }
            } else if (Instance != this) {
                Destroy(gameObject);
            }
        }

        protected virtual void OnFirstInit() { }
    }
}