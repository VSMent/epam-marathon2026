import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { AuthenticationService } from '../services/authentication.service';
import { UserModel } from '../models/user.model';

import { AdminGuard } from './admin.guard';

describe('AdminGuard', () => {
  let guard: AdminGuard;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    guard = TestBed.inject(AdminGuard);
  });

  it('should be created', () => {
    expect(guard).toBeTruthy();
  });

  describe('canActivate', () => {
    let authService: AuthenticationService;
    let router: Router;
    let navigateSpy: jasmine.Spy;

    beforeEach(() => {
      authService = TestBed.inject(AuthenticationService);
      router = TestBed.inject(Router);
      navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);
    });

    it('allows an Admin user', () => {
      spyOn(authService, 'getUser').and.returnValue({ role: 'Admin' } as UserModel);

      expect(guard.canActivate()).toBeTrue();
      expect(navigateSpy).not.toHaveBeenCalled();
    });

    it('redirects a non-admin user to /main', () => {
      spyOn(authService, 'getUser').and.returnValue({ role: 'Client' } as UserModel);

      expect(guard.canActivate()).toBeFalse();
      expect(navigateSpy).toHaveBeenCalledOnceWith(['/main']);
    });

    it('redirects an unauthenticated user to /login', () => {
      spyOn(authService, 'getUser').and.returnValue(null);

      expect(guard.canActivate()).toBeFalse();
      expect(navigateSpy).toHaveBeenCalledOnceWith(['/login']);
    });
  });
});
