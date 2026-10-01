const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const source = fs.readFileSync('AppServicios.Api/wwwroot/app.js', 'utf8');
const start = source.indexOf('async function runAdminPaymentApproval(');
const end = source.indexOf('function getCoordinationFilters(', start);
(async () => {
  for (const approved of [false, true]) {
    let feedback;
    const context = vm.createContext({ currentSession: { rol: 'Administrador' },
      setAdminFeedback: message => feedback = message,
      fetch: async () => ({ ok: true, json: async () => ({ pagoId: 15, aprobado: approved, estado: approved ? 'Aprobado' : 'Pendiente', message: 'Cobro pendiente en el proveedor.' }) }),
      loadCoordinationDashboard: async () => {}, loadProfessionalDashboard: async () => {} });
    vm.runInContext(source.slice(start, end), context);
    await context.runAdminPaymentApproval(15);
    assert.equal(feedback, approved ? 'Cobro verificado para el alta profesional. Pago #15.' : 'Cobro pendiente en el proveedor.');
  }
  console.log('PASS: administrator feedback never reports a pending provider payment as approved');
})().catch(error => { console.error(error); process.exitCode = 1; });
