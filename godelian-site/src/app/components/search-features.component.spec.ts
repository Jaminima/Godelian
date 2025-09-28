import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SearchFeaturesComponent } from './search-features.component';

describe('SearchFeaturesComponent', () => {
  let component: SearchFeaturesComponent;
  let fixture: ComponentFixture<SearchFeaturesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SearchFeaturesComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(SearchFeaturesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});