import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-button',
  standalone: true,
  template: `
    <button 
      [type]="type"
      [class]="'btn ' + getBtnClass() + ' ' + (className || '')"
      [disabled]="disabled"
      (click)="onClick($event)">
      <ng-content select="svg"></ng-content>
      <span><ng-content></ng-content></span>
    </button>
  `,
  styleUrls: ['./button.component.scss']
})
export class ButtonComponent {
  @Input() type: 'button' | 'submit' = 'button';
  @Input() active = false;
  @Input() disabled = false;
  @Input() className = '';
  @Input() variant: 'primary' | 'secondary' | 'success' | 'danger' | 'warning' | 'info' | 'light' | 'dark' = 'primary';
  @Input() outline = false;
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() onClick: (event: Event) => void = () => {};
  
  getBtnClass(): string {
    let classes = [];
    
    // Button variant
    if (this.outline) {
      classes.push(`btn-outline-${this.variant}`);
    } else {
      classes.push(`btn-${this.variant}`);
    }
    
    // Button size
    if (this.size === 'sm') {
      classes.push('btn-sm');
    } else if (this.size === 'lg') {
      classes.push('btn-lg');
    }
    
    // Active state
    if (this.active) {
      classes.push('active');
    }
    
    return classes.join(' ');
  }
}
