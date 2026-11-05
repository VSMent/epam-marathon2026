import { CanActivate, Router } from '@angular/router';
import { AuthenticationService } from '../services/authentication.service';
import { inject, Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AdminGuard implements CanActivate {
  private authService = inject(AuthenticationService);
  private router = inject(Router);

  canActivate(): boolean {
    const user = this.authService.getUser();

    if (!user || !user.role) {
      console.warn('User not authenticated or missing role');
      this.router.navigate(['/login']);
      return false;
    }

    if (user.role === 'Admin') {
      return true;
    }
    this.router.navigate(['/main']);
    return false;
  }
}
