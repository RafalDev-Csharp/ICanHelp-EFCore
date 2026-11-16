import { TestBed } from '@angular/core/testing';
import { EMPTY, of, throwError } from 'rxjs';

import { ToastService } from '../ui/toast.service';

import { ApiCommands } from './api-commands';

describe('ApiCommands', () => {
  const toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };
  let commands: ApiCommands;

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({ providers: [{ provide: ToastService, useValue: toast }] });
    commands = TestBed.inject(ApiCommands);
  });

  it('returns the response and shows the success message', async () => {
    await expect(commands.send(of({ id: 'o1' }), 'Oferta wysłana.')).resolves.toEqual({ result: { id: 'o1' } });
    expect(toast.success).toHaveBeenCalledWith('Oferta wysłana.');
  });

  it('treats an empty response (204) as a success', async () => {
    await expect(commands.run(EMPTY, 'Oferta przyjęta.')).resolves.toBe(true);
    expect(toast.success).toHaveBeenCalledWith('Oferta przyjęta.');
  });

  it('reports a failure without a message of its own (the interceptor showed one)', async () => {
    const refused = throwError(() => ({ status: 409 }));

    await expect(commands.run(refused, 'Oferta przyjęta.')).resolves.toBe(false);
    await expect(commands.send(refused, 'Oferta przyjęta.')).resolves.toBeNull();
    expect(toast.success).not.toHaveBeenCalled();
    expect(toast.error).not.toHaveBeenCalled();
  });
});
