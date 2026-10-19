import { ajax_service } from './ajax_service';

export function consultar_health() {
  return ajax_service.get('health', { timeout: 3000 });
}
