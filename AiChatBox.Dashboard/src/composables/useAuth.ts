export function useAuth() {
  interface UserProfile {
    username: string;
    email: string;
    role: 'SystemAdmin' | 'PartnerDeveloper' | 'StandardUser';
    partnerAccountId?: string;
  }

  function getUser(): UserProfile | null {
    const raw = localStorage.getItem('acb_user');
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
    localStorage.removeItem('acb_token');
    localStorage.removeItem('acb_user');
    window.location.href = '/login';
  }

  return { getUser, getUserRole, isAdmin, isPartner, isStandard, logout };
}
