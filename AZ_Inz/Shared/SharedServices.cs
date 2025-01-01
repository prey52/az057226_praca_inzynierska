namespace AZ_Inz.Shared
{
    public class AuthenticationStateService
    {
        public bool IsAuthenticated { get; private set; }

        public event Action? AuthenticationStateChanged;

        public void UpdateAuthenticationState(bool isAuthenticated)
        {
            IsAuthenticated = isAuthenticated;
            NotifyAuthenticationStateChanged();
        }

        private void NotifyAuthenticationStateChanged()
        {
            AuthenticationStateChanged?.Invoke();
        }


    }


}
