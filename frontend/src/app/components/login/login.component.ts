import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth.service';

interface DemoUser {
  label: string;
  userId: number;
  password: string;
  description: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
})
export class LoginComponent {
  @Output() loggedIn = new EventEmitter<void>();

  readonly demoUsers: DemoUser[] = [
    { label: 'משתמש 1',  userId: 1, password: 'user1',  description: 'רואה רק בקשות בבעלותו או מוקצות אליו' },
    { label: 'משתמש 2',  userId: 2, password: 'user2',  description: 'רואה רק בקשות בבעלותו או מוקצות אליו' },
    { label: 'מנהל',     userId: 3, password: 'admin',  description: 'רואה את כל הבקשות' },
  ];

  loading = false;
  error: string | null = null;

  constructor(private authService: AuthService) {}

  login(user: DemoUser): void {
    this.loading = true;
    this.error = null;

    this.authService.login(user.userId, user.password).subscribe({
      next: () => {
        this.loading = false;
        this.loggedIn.emit();
      },
      error: () => {
        this.loading = false;
        this.error = 'שגיאה בהתחברות — נסה שוב';
      },
    });
  }
}
