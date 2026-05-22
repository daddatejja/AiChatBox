export function useAuth() {
  interface UserProfile {
    username: string;
    email: string;
    role: 'SystemAdmin' | 'PartnerDeveloper' | 'StandardUser';
    partnerAccountId?: string;
    impersonatedBy?: string;
  }

  function getUser(): UserProfile | null {
    const raw = sessionStorage.getItem('acb_user') || localStorage.getItem('acb_user');
    return raw ? JSON.parse(raw) as UserProfile : null;
  }

  function getUserRole(): 'SystemAdmin' | 'PartnerDeveloper' | 'StandardUser' {
    return getUser()?.role || 'StandardUser';
  }

  function isAdmin(): boolean {
    return getUserRole() === 'SystemAdmin';
  }

  function isPartner(): boolean {
    return getUserRole() === 'PartnerDeveloper';
  }

  function isStandard(): boolean {
    return getUserRole() === 'StandardUser';
  }

  function logout() {
    sessionStorage.removeItem('acb_token');
    sessionStorage.removeItem('acb_user');
    sessionStorage.removeItem('acb_username');
    sessionStorage.removeItem('acb_is_impersonating');
    localStorage.removeItem('acb_token');
    localStorage.removeItem('acb_user');
    localStorage.removeItem('acb_username');
    window.location.href = '/login';
  }

  function exitImpersonation() {
    sessionStorage.removeItem('acb_token');
    sessionStorage.removeItem('acb_user');
    sessionStorage.removeItem('acb_username');
    sessionStorage.removeItem('acb_is_impersonating');
    // Try to close window first if opened via window.open
    try {
      window.close();
    } catch (e) {
      console.error("Failed to close window", e);
    }
    // Fallback redirect
    setTimeout(() => {
      window.location.href = '/';
    }, 100);
  }

  return { getUser, getUserRole, isAdmin, isPartner, isStandard, logout, exitImpersonation };
}
