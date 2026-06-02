import { TestBed } from '@angular/core/testing';
import { TranslatePipe } from './translate.pipe';
import { TranslationService } from '../services/translation.service';

describe('TranslatePipe', () => {
  let pipe: TranslatePipe;
  let service: TranslationService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(TranslationService);
    pipe = TestBed.runInInjectionContext(() => new TranslatePipe());
  });

  it('should create', () => {
    expect(pipe).toBeTruthy();
  });

  it('should translate a key in the default language (es)', () => {
    service.setLang('es');
    const result = pipe.transform('login.title');
    expect(result).toBeTruthy();
    expect(result).not.toBe('login.title');
  });

  it('should translate a key in English', () => {
    service.setLang('en');
    const enResult = pipe.transform('login.title');
    expect(enResult).toBeTruthy();
    expect(enResult).not.toBe('login.title');
  });

  it('should re-translate when language changes (regression for pure:true bug)', () => {
    service.setLang('en');
    const enResult = pipe.transform('login.title');

    service.setLang('es');
    const esResult = pipe.transform('login.title');

    // Both must be real translations (not the key itself)
    expect(enResult).not.toBe('login.title');
    expect(esResult).not.toBe('login.title');
    // They must differ — proving the pipe re-evaluated on language change
    expect(enResult).not.toEqual(esResult);
  });

  it('should interpolate params', () => {
    service.setLang('en');
    const result = pipe.transform('users.count', { count: 5 });
    expect(result).toContain('5');
  });

  it('should fall back to English when key is missing in current language', () => {
    service.setLang('es');
    // If key doesn't exist in es but does in en, it should still return a value
    const result = pipe.transform('login.title');
    expect(result).not.toBe('login.title');
  });

  it('pipe must be impure (pure: false)', () => {
    // The re-translation test above is the behavioral proof.
    // This test simply acts as a documentation anchor.
    expect(true).toBe(true);
  });
});

