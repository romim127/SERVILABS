"""End-to-end regression: isolated PostgreSQL + real API + Chrome.
Requires: pip install playwright psycopg[binary]; installed Chrome, .NET 10 and PostgreSQL.
Run after dotnet build: python tests/auth_flow.py
Never connects to the application database or payment providers.
"""
import copy
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
from playwright.sync_api import sync_playwright, expect
import psycopg
from profile_line_flow import Operator, run_checks

ROOT = Path(__file__).resolve().parents[1]
PG = Path(os.environ.get('PG_BIN', 'C:/Program Files/PostgreSQL/18/bin'))

def free_port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        return sock.getsockname()[1]

def check(condition, message):
    assert condition, message
    print('PASS:', message, flush=True)

def run():
    temp = Path(tempfile.mkdtemp(prefix='servilabs-auth-'))
    dbport, port = free_port(), free_port()
    base = f'http://127.0.0.1:{port}'
    operator = Operator()
    process = None
    db_started = False
    logfile = (temp / 'api.log').open('w', encoding='utf-8')
    try:
        subprocess.run([str(PG / 'initdb.exe'), '-D', str(temp / 'db'), '-U', 'postgres', '-A', 'trust', '--encoding=UTF8', '--no-locale'], check=True, capture_output=True)
        subprocess.run([str(PG / 'pg_ctl.exe'), '-D', str(temp / 'db'), '-l', str(temp / 'postgres.log'), '-o', f'-h 127.0.0.1 -p {dbport}', '-w', 'start'], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
        db_started = True
        env = dict(os.environ)
        env.update({
            'ASPNETCORE_ENVIRONMENT': 'Development',
            'ASPNETCORE_URLS': base,
            'ConnectionStrings__DefaultConnection': f'Host=127.0.0.1;Port={dbport};Database=postgres;Username=postgres',
            'SuperAdmin__Email': 'identity-admin@example.test', 'SuperAdmin__Password': '',
            'MercadoPago__AccessToken': '', 'MercadoPago__PublicKey': '',
            'VAPID__PublicKey': '', 'VAPID__PrivateKey': '',
            'OpenAI__ApiKey': '', 'OPENAI_API_KEY': '',
            'Logging__LogLevel__Default': 'Warning'
        })
        env.update(operator.env(base))
        process = subprocess.Popen(['dotnet', str(ROOT / 'AppServicios.Api/bin/Debug/net10.0/AppServicios.Api.dll')], cwd=ROOT / 'AppServicios.Api', env=env, stdout=logfile, stderr=logfile)
        def api(path, data=None, token=None, method=None):
            headers = {'Content-Type': 'application/json'}
            if token: headers['Authorization'] = 'Bearer ' + token
            req = urllib.request.Request(base + path, data=json.dumps(data).encode() if data is not None else None, headers=headers, method=method)
            try: response = urllib.request.urlopen(req, timeout=15)
            except urllib.error.HTTPError as exc: response = exc
            body = response.read().decode()
            return response.status, json.loads(body) if 'json' in response.headers.get('Content-Type', '') else body
        for _ in range(100):
            try:
                if api('/health')[0] == 200: break
            except (OSError, urllib.error.URLError): pass
            time.sleep(.2)
        else: raise AssertionError('Local API did not start: ' + (temp / 'api.log').read_text()[-3000:])
        for description, expected in [('Mi cocina a gas no enciende', 'cocina'), ('El calefón no enciende', 'calefón'), ('La caldera a gas falla', 'caldera'), ('Hay olor en la cocina', None), ('Busco gastronomía', None)]:
            boundary = 'servilabs-test-boundary'
            body = (f'--{boundary}\r\nContent-Disposition: form-data; name="description"\r\n\r\n{description}\r\n--{boundary}--\r\n').encode()
            req = urllib.request.Request(base + '/api/Ai/cosa-del-cosito', data=body, headers={'Content-Type': 'multipart/form-data; boundary=' + boundary})
            with urllib.request.urlopen(req) as response:
                suggestion = json.load(response)
            check(expected in suggestion['suggestedService'].lower() if expected else not suggestion['suggestedPost'], 'ASI preserves appliance or asks for clarification: ' + description + ' => ' + str(suggestion))
        def payload(email='test@example.test', dni='90000001'):
            return {'usuario': {'nombre': 'Cliente Prueba', 'email': email, 'telefono': '1122334455', 'dni': dni, 'fechaNacimiento': '1990-01-01T00:00:00Z', 'rol': 'Cliente', 'passwordHash': ' Test-password-123 ', 'activo': True}, 'ubicacion': 'CABA Argentina', 'latitud': -34.6, 'longitud': -58.4, 'preferencias': ''}
        data = payload()
        status, session = api('/api/Auth/register-client', data)
        if status != 200: print('REGISTRATION RESPONSE', status, str(session)[:3500], flush=True)
        check(status == 200 and session.get('clienteId') and session.get('accessToken'), 'API creates user, client and authenticated session')
        token = session['accessToken']
        check(api('/api/Billetera/pagos-protegidos/999999/confirmar-pago-demo', {'usuarioOperadorId':session['usuarioId'],'detalle':'security test'},token=token)[0]==410, 'Demo payment confirmation is retired in every environment')
        status, again = api('/api/Auth/register-client', data)
        check(status == 200 and again['clienteId'] == session['clienteId'], 'Retry uses the same account and client')
        bad = copy.deepcopy(data); bad['usuario']['passwordHash'] = 'wrong-password'
        check(api('/api/Auth/register-client', bad)[0] == 409, 'Duplicate with wrong password cannot take over the account')
        check(api('/api/Auth/login', {'email': 'TEST@EXAMPLE.TEST', 'password': data['usuario']['passwordHash']})[0] == 200, 'Login supports email case and preserves password whitespace')
        check(api(f"/api/Auth/usuarios/{session['usuarioId']}/context")[0] == 401, 'Session context requires authentication')
        check(api(f"/api/Auth/usuarios/{session['usuarioId']}/context", token=token)[0] == 200, 'Owner can restore session context')
        check(api('/api/Auth/usuarios/99999/context', token=token)[0] == 403, 'Client cannot read another session')
        check(api('/api/does-not-exist')[0] == 404, 'Unknown API routes return 404 instead of HTML')
        partial = payload('partial@example.test', '90000002')
        partial['usuario']['verificadoRenaper'] = True
        status, user = api('/api/Usuarios', partial['usuario'])
        check(status == 201, 'Legacy interrupted signup fixture created')
        check(user['verificadoRenaper'] is False, 'Public signup cannot self-verify identity')
        status, resumed = api('/api/Auth/register-client', partial)
        check(status == 200 and resumed['usuarioId'] == user['id'] and resumed['clienteId'], 'Interrupted legacy registration completes safely')
        import base64
        png = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')
        def upload_identity(auth, image=png):
            boundary = 'identity-test-boundary'
            body = b''
            for field in ['foto', 'dni']:
                body += (f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="test.png"\r\nContent-Type: image/png\r\n\r\n').encode() + image + b'\r\n'
            body += f'--{boundary}--\r\n'.encode()
            headers = {'Content-Type': 'multipart/form-data; boundary=' + boundary}
            if auth: headers['Authorization'] = 'Bearer ' + auth
            req = urllib.request.Request(base + '/api/Identidad', data=body, headers=headers)
            try: response = urllib.request.urlopen(req)
            except urllib.error.HTTPError as error: response = error
            return response.status
        check(upload_identity(None) == 401, 'Identity upload requires login')
        check(upload_identity(token, b'not-an-image') == 400, 'Invalid image rejected even with image content type')
        check(upload_identity(token) == 200, 'Authenticated client submits photo and DNI')
        state = api('/api/Identidad', token=token)[1]
        check(state == {'presentada': True, 'verificada': False}, 'Uploaded identity stays unverified pending review')
        check(api(f"/api/Identidad/{session['usuarioId']}/foto", token=resumed['accessToken'])[0] == 403, 'Other accounts cannot read identity photos')
        own_image = urllib.request.urlopen(urllib.request.Request(base + f"/api/Identidad/{session['usuarioId']}/foto", headers={'Authorization': 'Bearer ' + token}))
        check(own_image.read() == png and own_image.headers['Cache-Control'] == 'no-store', 'Owner photo is private and not cached')
        admin_data = payload('identity-admin@example.test', '90000009')
        admin = api('/api/Auth/register-client', admin_data)[1]
        device = {'token': 'test-native-device-token-1234567890'}
        check(api('/api/Push/native-token', device)[0] == 401, 'Native registration requires authentication')
        check(api('/api/Push/native-token', {'token': 'bad'}, token=token)[0] == 400, 'Invalid native token rejected')
        check(api('/api/Push/native-status', token=token)[1]['configured'] is False, 'Missing Firebase server credential is reported honestly')
        check(api('/api/Push/native-token', device, token=token)[0] == 200, 'Client registers a native device')
        check(api('/api/Push/native-token', device, token=token)[0] == 200, 'Native registration is idempotent')
        check(api('/api/Push/native-token', device, token=resumed['accessToken'])[0] == 200, 'Device can follow the currently signed-in account')
        check(api('/api/Push/native-unsubscribe', device, token=token)[0] == 200, 'Previous account unsubscribe cannot remove the new owner')
        with psycopg.connect(host='127.0.0.1', port=dbport, dbname='postgres', user='postgres', autocommit=True) as db:
            db.execute('UPDATE "Usuarios" SET "Rol" = %s WHERE "Id" = %s', ('Administrador', admin['usuarioId']))
            admin_token = api('/api/Auth/login', {'email': admin_data['usuario']['email'], 'password': admin_data['usuario']['passwordHash']})[1]['accessToken']
            approve = {'adminUserId': admin['usuarioId'], 'verificadoRenaper': True, 'motivo': 'Test manual document review'}
            check(api(f"/api/Coordinacion/admin/usuarios/{resumed['usuarioId']}/accion", approve, token=admin_token)[0] == 400, 'Admin cannot verify a user without documents')
            check(api(f"/api/Coordinacion/admin/usuarios/{session['usuarioId']}/accion", approve, token=token)[0] == 403, 'Client cannot approve identity')
            check(api(f"/api/Coordinacion/admin/usuarios/{session['usuarioId']}/accion", approve, token=admin_token)[0] == 200, 'Authorized admin reviews and approves submitted identity')
            check(api('/api/Identidad', token=token)[1]['verificada'] is True, 'Reviewed identity is verified')
            check(api(f"/api/Auth/usuarios/{session['usuarioId']}/context", token=token)[1]['identidadVerificada'] is True, 'Session exposes verified identity')
            check(upload_identity(token) == 200 and api('/api/Identidad', token=token)[1]['verificada'] is False, 'Replacing documents requires a new review')
            check(api('/api/Identidad', token=token, method='DELETE')[0] == 204, 'Owner can delete identity documents')
            check(api('/api/Identidad', token=token)[1] == {'presentada': False, 'verificada': False}, 'Deletion resets verification')
            owners = db.execute('SELECT "UsuarioId" FROM "PushSubscriptions" WHERE "Endpoint" = %s', ('fcm:' + device['token'],)).fetchall()
            check(owners == [(resumed['usuarioId'],)], 'Only the current account owns the device subscription')
            check(api('/api/Push/native-unsubscribe', device, token=resumed['accessToken'])[0] == 200, 'Owner can disable native notifications on logout')
            check(db.execute('SELECT count(*) FROM "PushSubscriptions" WHERE "Endpoint" = %s', ('fcm:' + device['token'],)).fetchone()[0] == 0, 'Logout removes the native subscription')
            db.execute("CREATE FUNCTION reject_test_client() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Ubicacion\" = 'FORCE_ROLLBACK' THEN RAISE EXCEPTION 'test failure'; END IF; RETURN NEW; END $$")
            db.execute('CREATE TRIGGER reject_test_client BEFORE INSERT ON "Clientes" FOR EACH ROW EXECUTE FUNCTION reject_test_client()')
            rollback = payload('rollback@example.test', '90000003'); rollback['ubicacion'] = 'FORCE_ROLLBACK'
            check(api('/api/Auth/register-client', rollback)[0] == 500, 'Simulated profile database failure reaches API')
            count = db.execute('SELECT count(*) FROM "Usuarios" WHERE "Email" = %s', ('rollback@example.test',)).fetchone()[0]
            check(count == 0, 'Profile failure rolls back user creation')
            db.execute('DROP TRIGGER reject_test_client ON "Clientes"')
            db.execute('DROP FUNCTION reject_test_client()')
        with psycopg.connect(host='127.0.0.1', port=dbport, dbname='postgres', user='postgres', autocommit=True) as db:
            run_checks(base,api,token,session,resumed['accessToken'],png,operator,db,check)
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(channel='chrome', headless=True)
            context = browser.new_context(viewport={'width': 412, 'height': 915}, service_workers='block')
            context.route('https://**/*', lambda route: route.abort())
            page = context.new_page()
            errors = []
            page.on('pageerror', lambda error: errors.append(str(error)))
            page.goto(base, wait_until='domcontentloaded')
            expect(page.locator('#wizardEntry')).to_be_visible()
            page.locator('#wizardEmail').fill('missing@example.test')
            page.locator('#wizardPassword').fill('wrong-password')
            page.locator('#wizardFinishBtn').click()
            expect(page.locator('#wizardLoginFeedback')).to_contain_text('No se pudo iniciar')
            check(page.locator('#wizardLoginFeedback').is_visible(), 'Login failure is visible on the entry screen')
            page.locator('#wizardEmail').fill('browser@example.test')
            page.locator('#wizardPassword').fill('Browser-pass-123')
            page.locator('#goToRegister').click()
            expect(page.locator('#registerEmailInput')).to_have_value('browser@example.test')
            expect(page.locator('#registerPasswordInput')).to_have_value('Browser-pass-123')
            check(page.locator('#registerNameInput').input_value() == '', 'Signup has no prefilled demo identity')
            for selector, value in {'#registerNameInput': 'Prueba Navegador', '#registerPhoneInput': '1122334455', '#registerDniInput': '90000004', '#registerBirthDateInput': '1990-01-01', '#registerLocationInput': 'CABA Argentina'}.items():
                page.locator(selector).fill(value)
            page.locator('#registerDniInput').fill('123')
            page.locator('#registerContinueButton').click()
            expect(page.locator('#registerFeedback')).to_contain_text('DNI debe contener', timeout=15000)
            check(page.locator('#registerContinueButton').is_enabled(), 'Validation errors are visible and permit correction')
            page.locator('#registerDniInput').fill('90000004')
            page.locator('#registerContinueButton').click()
            expect(page.locator('#dashboard-cliente')).to_be_visible(timeout=15000)
            stored = page.evaluate("JSON.parse(localStorage.getItem('appservicios-session'))")
            check(bool(stored.get('accessToken') and stored.get('clienteId')), 'Mobile browser registration opens client dashboard with credentials')
            page.reload(wait_until='domcontentloaded')
            expect(page.locator('#dashboard-cliente')).to_be_visible(timeout=15000)
            restored = page.evaluate("JSON.parse(localStorage.getItem('appservicios-session'))")
            check(restored['accessToken'] == stored['accessToken'], 'Reload restores session without discarding token')
            expect(page.locator('#requestClientSelect')).to_have_value(str(stored['clienteId']), timeout=15000)
            expect(page.locator('#acceso')).to_be_hidden()
            expect(page.locator('#cuenta > details > summary')).to_be_visible()
            expect(page.locator('#identityBadge')).to_contain_text('Cliente no verificado')
            page.locator('#openAccountButton').click()
            expect(page.locator('#cuenta > details')).to_have_attribute('open', '')
            check(page.locator('#leaveAppButton').count() == 0, 'Only account logout remains')
            expect(page.locator('#accountAvatar')).to_contain_text('PN')
            check(page.locator('#showPanelsButton').count()==0,'Header shows Mi cuenta with avatar without a redundant Paneles button')
            page.locator('#publicPhoto').set_input_files({'name':'profile.png','mimeType':'image/png','buffer':png})
            page.locator('#publicPhotoForm button[type=submit]').click()
            expect(page.locator('#publicPhotoStatus')).to_contain_text('se guardó')
            expect(page.locator('#accountAvatar img')).to_have_count(1)
            page.locator('#viewOwnProfile').click()
            expect(page.locator('.public-profile-dialog')).to_be_visible()
            expect(page.locator('.public-profile-content')).to_contain_text('Prueba Navegador')
            expect(page.locator('.public-profile-content')).to_contain_text('Cliente no verificado')
            page.locator('.profile-back').click()
            page.locator('#deletePublicPhoto').click()
            expect(page.locator('#publicPhotoStatus')).to_contain_text('Foto eliminada')
            expect(page.locator('#accountAvatar img')).to_have_count(0)
            page.locator('.identity-security > summary').filter(has_text='Celular').click()
            expect(page.locator('#profilePhoneNumber')).to_have_value('1122334455')
            page.locator('#lineConsent').check()
            page.locator('[data-line-check="numero"]').click()
            page.wait_for_url('**/#cuenta',timeout=15000)
            expect(page.locator('#lineChecks')).to_contain_text('Coincidencia confirmada',timeout=15000)
            expect(page.locator('#lineChecks')).to_contain_text('no acredita verificación real')
            check(True,'Browser verification returns to Mi cuenta with session intact and honest sandbox result')
            page.locator('.identity-security > summary').filter(has_text='Celular').click()

            page.locator('.identity-security > summary').filter(has_text='Seguridad').click()
            for selector in ['#identityPhoto', '#identityDni']:
                page.locator(selector).set_input_files({'name': 'test.png', 'mimeType': 'image/png', 'buffer': png})
            page.locator('#identitySubmit').click()
            expect(page.locator('#identityStatus')).to_contain_text('pendientes de revisión')
            expect(page.locator('#identityBadge')).to_contain_text('Cliente no verificado')
            check(True, 'Security form submits photo and DNI without automatically verifying the client')

            page.locator('#cuenta > details > summary').click()
            expect(page.locator('.app-panel[open]')).to_have_count(0)
            for width in [320, 390, 768]:
                page.set_viewport_size({'width': width, 'height': 915})
                check(page.evaluate('document.documentElement.scrollWidth <= window.innerWidth'), f'Panel overview fits {width}px without horizontal scrolling')
            page.set_viewport_size({'width': 412, 'height': 915})
            page.screenshot(path=str(Path(tempfile.gettempdir()) / 'servilabs-panels.png'))
            page.locator('#dashboard-cliente > details > summary').click()
            expect(page.locator('#requestServiceSearch')).to_be_visible()
            check(page.locator('.app-panel[open]').count() == 1, 'Only the selected functional panel is expanded')
            page.set_viewport_size({'width': 320, 'height': 915})
            check(page.evaluate('document.documentElement.scrollWidth <= window.innerWidth'), 'Expanded request panel fits a small mobile screen')
            page.set_viewport_size({'width': 412, 'height': 915})
            search = page.locator('#requestServiceSearch')
            search.fill('PLÓMERO')
            expect(page.locator('#requestServiceSelect option')).to_have_count(5)
            expect(page.locator('#requestServiceSelect')).to_contain_text('Plomería')
            search.fill('gasista')
            expect(page.locator('#requestServiceSelect option')).to_have_count(2)
            expect(page.locator('#requestServiceSelect')).to_contain_text('Gasista')
            search.fill('zzzz-no-existe')
            expect(page.locator('#requestServiceSelect option')).to_have_count(1)
            expect(page.locator('#serviceSearchFeedback')).to_contain_text('No encontramos')
            search.fill('')
            expect(page.locator('#requestServiceSelect option')).to_have_count(70)
            search.fill('plomeria')
            page.locator('#requestServiceSelect').select_option('5')
            page.evaluate('fillRequestSelectors()')
            expect(page.locator('#requestServiceSelect')).to_have_value('5')
            check(True, 'Full catalogue, accented aliases, Gasista, no-results and preserved selection work')
            page.locator('#requestBudgetInput').fill('5000')
            page.locator('#requestLocationInput').fill('CABA Argentina')
            page.locator('#requestDescriptionInput').fill('Necesito reparar una instalación eléctrica de prueba.')
            with page.expect_response(lambda response: response.url.endswith('/api/SolicitudesTrabajo') and response.request.method == 'POST') as published:
                page.locator('#requestSubmitButton').click()
            if published.value.status != 201: print('REQUEST ERROR', published.value.status, published.value.text()[:1500], flush=True)
            expect(page.locator('#requestFeedback')).to_contain_text('creada correctamente', timeout=15000)
            check(True, 'Registered client publishes a service request')
            page.locator('#chat-solicitud > details > summary').click()
            page.locator('#chatMessageInput').fill('Mensaje de prueba para coordinar el servicio')
            page.locator('#chatSendButton').click()
            expect(page.locator('#chatMessagesList')).to_contain_text('Mensaje de prueba para coordinar el servicio', timeout=15000)
            check(True, 'Client sends and reads a request chat message')
            check(api(f"/api/Billetera/usuario/{stored['usuarioId']}", token=stored['accessToken'])[0] == 200, 'Wallet loads using the restored session')
            page.locator('#openAccountButton').click()
            page.locator('#logoutButton').click()
            expect(page.locator('#cuenta')).to_be_hidden()
            expect(page.locator('#wizardEntry')).to_be_visible()
            check(page.evaluate('window.scrollY') == 0, 'Logout returns to the main access screen at the top')
            expect(page.locator('#wizardEntry')).to_be_visible()
            page.locator('#wizardEmail').fill('browser@example.test')
            page.locator('#wizardPassword').fill('Browser-pass-123')
            page.locator('#wizardFinishBtn').click()
            expect(page.locator('#dashboard-cliente')).to_be_visible(timeout=15000)
            page.locator('#openAccountButton').click()
            page.locator('#logoutButton').click()
            expect(page.locator('#cuenta')).to_be_hidden()
            expect(page.locator('#wizardEntry')).to_be_visible()
            check(page.evaluate('window.scrollY') == 0, 'Logout returns to the main access screen at the top')
            page.locator('#goToRegister').click()
            for selector, value in {'#registerNameInput': 'Profesional Prueba', '#registerEmailInput': 'professional@example.test', '#registerPasswordInput': 'Professional-pass-123', '#registerPhoneInput': '1122334455', '#registerDniInput': '90000005', '#registerBirthDateInput': '1990-01-01', '#registerLocationInput': 'CABA Argentina'}.items():
                page.locator(selector).fill(value)
            page.locator('[data-account-role="profesional"]').click()
            page.locator('#termsCheckbox').check()
            with page.expect_response(lambda response: '/mercadopago/preference' in response.url) as preference_response:
                page.locator('#startPaymentButton').click()
            check(preference_response.value.status == 400, 'Professional payment request is authenticated (provider intentionally unconfigured)')
            professional = page.evaluate("JSON.parse(localStorage.getItem('appservicios-session'))")
            check(professional['rol'] == 'Profesional' and professional.get('accessToken'), 'Professional signup obtains credentials before protected calls')
            # Simulate payment approval only in this isolated database; never contact the provider.
            with psycopg.connect(host='127.0.0.1', port=dbport, dbname='postgres', user='postgres', autocommit=True) as db:
                db.execute('UPDATE "PagosProfesionales" SET "Estado" = %s, "FechaAprobacion" = now() WHERE "UsuarioId" = %s', ('Aprobado', professional['usuarioId']))
            page.route('**/mercadopago/verificar', lambda route: route.fulfill(status=200, content_type='application/json', body=json.dumps({'aprobado': True, 'estado': 'Aprobado'})))
            page.locator('#verifyPaymentButton').click()
            expect(page.locator('#registerContinueButton')).to_be_enabled()
            page.route('**/api/Profesionales', lambda route: route.fulfill(status=503, content_type='application/json', body=json.dumps({'message': 'Fallo temporal de prueba'})), times=1)
            page.locator('#registerContinueButton').click()
            expect(page.locator('#registerFeedback')).to_be_visible()
            expect(page.locator('#registerFeedback')).to_contain_text('Fallo temporal de prueba')
            check(not page.locator('#dashboard-profesional').is_visible(), 'Failed professional activation stays on the form with a visible error')
            page.locator('#registerContinueButton').click()
            expect(page.locator('#dashboard-profesional')).to_be_visible(timeout=15000)
            profile = page.evaluate("JSON.parse(localStorage.getItem('appservicios-session'))")
            check(bool(profile.get('profesionalId') and profile.get('accessToken')), 'Professional profile activation retains its access token')
            page.reload(wait_until='domcontentloaded')
            expect(page.locator('#dashboard-profesional')).to_be_visible(timeout=15000)
            check(True, 'Professional dashboard restores after reload')
            check(not errors, 'No JavaScript runtime errors: ' + repr(errors))
            browser.close()
    finally:
        operator.close()
        if process:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill(); process.wait()
        logfile.close()
        if db_started:
            subprocess.run([str(PG / 'pg_ctl.exe'), '-D', str(temp / 'db'), '-m', 'immediate', '-w', 'stop'], capture_output=True)
        # Only the unique temporary directory created above can be removed.
        if temp.parent == Path(tempfile.gettempdir()) and temp.name.startswith('servilabs-auth-'):
            shutil.rmtree(temp, ignore_errors=True)

if __name__ == '__main__':
    run()
