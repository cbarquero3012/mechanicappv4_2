import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn) {
    return true;
  }
  // Token missing or expired — redirect to login
  authService.logout();
  // Extract slug from the destination URL so deep-links work on fresh page loads
  const knownRoutes = ['login', 'landing', 'onboarding', 'subscription', 'dashboard', 'cars', 'customers', 'mechanics', 'repair-orders', 'payments', 'inventory', 'settings', 'currencies', 'users', 'tenants', 'user-guide'];
  const firstSegment = state.url.split('/').filter(Boolean)[0];
  const slug =
    (firstSegment && !knownRoutes.includes(firstSegment) ? firstSegment : null) ||
    localStorage.getItem('tenant_slug');
  router.navigate([slug ? `/${slug}/login` : '/landing']);
  return false;
};
