"""Isolated PostgreSQL, real HTTP API and fake Mercado Pago/Turnstile. Never uses production credentials."""
import copy, json, os, shutil, subprocess, tempfile, threading, time
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs
from concurrent.futures import ThreadPoolExecutor
import requests, psycopg
from auth_flow import PG, ROOT, free_port, check

class Provider:
    def __init__(self):
        self.payments = {}; self.refunds = {}; self.fail_refund = False; self.used = set()
        state = self
        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def send(self, data, status=200):
                self.send_response(status); self.send_header('Content-Type','application/json'); self.end_headers(); self.wfile.write(json.dumps(data).encode())
            def do_GET(self):
                path = urlparse(self.path)
                if path.path == '/users/me': return self.send({'id': 777})
                if path.path == '/v1/payments/search':
                    ref = parse_qs(path.query)['external_reference'][0]
                    return self.send({'results': [p for p in state.payments.values() if p.get('search_ref',p['external_reference']) == ref]})
                if path.path.startswith('/v1/payments/'):
                    return self.send(state.payments.get(path.path.split('/')[-1],{}))
                self.send({},404)
            def do_POST(self):
                body = self.rfile.read(int(self.headers.get('Content-Length','0')))
                if self.path == '/turnstile':
                    token = parse_qs(body.decode()).get('response',[''])[0]
                    good = token.startswith('valid-') and token not in state.used
                    state.used.add(token)
                    return self.send({'success': good, 'hostname': 'wrong.test' if 'wrong-host' in token else 'appservicios-mn6i.onrender.com', 'action': 'register' if 'register' in token else 'login'})
                if self.path.endswith('/refunds'):
                    if state.fail_refund: return self.send({'error':'failed'},502)
                    key = self.headers.get('X-Idempotency-Key'); assert key
                    state.refunds[key] = state.refunds.get(key,0)+1
                    payment = state.payments[self.path.split('/')[-2]]
                    payment.update(status='refunded', transaction_amount_refunded=payment['transaction_amount'])
                    return self.send({'status':'approved'},201)
                self.send({},400)
        self.server = ThreadingHTTPServer(('127.0.0.1',0),Handler)
        threading.Thread(target=self.server.serve_forever,daemon=True).start()
        self.url = 'http://127.0.0.1:'+str(self.server.server_port)
    def add(self, order, number):
        p={'id':number,'external_reference':order['referenciaExterna'],'currency_id':order['moneda'],
           'transaction_amount':order.get('montoBruto',order.get('monto')), 'transaction_amount_refunded':0,
           'collector_id':777,'live_mode':True,'status':'approved'}
        self.payments[str(number)]=p
        return p

def run():
    temp = Path(tempfile.mkdtemp(prefix='servilabs-payments-')); dbport,port=free_port(),free_port()
    provider=Provider(); process=None; started=False; logfile=(temp/'api.log').open('w',encoding='utf-8')
    base=f'http://127.0.0.1:{port}'; conn=f'host=127.0.0.1 port={dbport} dbname=postgres user=postgres'
    env=dict(os.environ); env.update({'ASPNETCORE_ENVIRONMENT':'Development','ASPNETCORE_URLS':base,
       'ConnectionStrings__DefaultConnection':f'Host=127.0.0.1;Port={dbport};Database=postgres;Username=postgres',
       'SuperAdmin__Email':'','SuperAdmin__Password':'','MercadoPago__AccessToken':'test-token',
       'MercadoPago__BaseUrl':provider.url,'MercadoPago__UseSandbox':'false','Turnstile__SiteKey':'',
       'Turnstile__SecretKey':'','Turnstile__Enabled':'false','Logging__LogLevel__Default':'Warning',
       'Jwt__Key':'isolated-test-secret-at-least-32-characters','Cors__AllowedOrigins__0':base})
    def api(path,data=None,session=None,method=None,headers=None):
        h=dict(headers or {})
        if session: h['Authorization']='Bearer '+session['accessToken']
        r=requests.request(method or ('POST' if data is not None else 'GET'),base+path,json=data,headers=h,timeout=35)
        try: body=r.json()
        except ValueError: body=r.text
        return r.status_code,body
    def login(email,password):
        status,s=api('/api/Auth/login',{'email':email,'password':password}); assert status==200,(status,s); return s
    def start():
        nonlocal process
        process=subprocess.Popen(['dotnet',str(ROOT/'AppServicios.Api/bin/Debug/net10.0/AppServicios.Api.dll')],cwd=ROOT/'AppServicios.Api',env=env,stdout=logfile,stderr=logfile)
        for _ in range(150):
            try:
                if api('/health')[0]==200:return
            except requests.RequestException:pass
            time.sleep(.2)
        raise AssertionError((temp/'api.log').read_text()[-5000:])
    def stop():
        nonlocal process
        if process: process.terminate(); process.wait(timeout=15); process=None
    def sql(statement,parameters=(),fetch=False):
        with psycopg.connect(conn,autocommit=True) as db:
            c=db.execute(statement,parameters)
            return c.fetchall() if fetch else None
    try:
        subprocess.run([str(PG/'initdb.exe'),'-D',str(temp/'db'),'-U','postgres','-A','trust','--encoding=UTF8','--no-locale'],check=True,capture_output=True)
        subprocess.run([str(PG/'pg_ctl.exe'),'-D',str(temp/'db'),'-l',str(temp/'postgres.log'),'-o',f'-h 127.0.0.1 -p {dbport}','-w','start'],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=30);started=True
        start()
        def register(n):
            data={'usuario':{'nombre':'Test Cliente','email':f'payment{n}@example.test','telefono':'1122334455','dni':str(91000000+n),'fechaNacimiento':'1990-01-01T00:00:00Z','rol':'Cliente','passwordHash':'Test-password-123'},'ubicacion':'Buenos Aires','preferencias':''}
            status,s=api('/api/Auth/register-client',data);assert status==200,(status,s);return s
        client,other=register(1),register(2)
        pro=login('profesional@servilab.com','Profesional123!');admin=login('admin@appservicios.com','Admin123!')
        def act(session):return {'usuarioOperadorId':session['usuarioId'],'detalle':'Prueba aislada'}
        check(api('/api/Billetera/pagos-protegidos/1/confirmar-pago-demo',act(client),client)[0]==410,'Demo confirmation is retired')
        def order():
            # Seed accepted jobs, but exercise order creation and all money operations through the API.
            sid=sql('INSERT INTO "SolicitudesTrabajo" ("ClienteId","ProfesionalId","ServicioId","Latitud","Longitud","Ubicacion","Descripcion","FechaRequerida","PresupuestoEstimado","Estado","FechaCreacion") VALUES (%s,%s,1,0,0,%s,%s,now(),1000,%s,now()) RETURNING "Id"',(client['clienteId'],pro['profesionalId'],'Test location','Isolated payment test','Aceptado'),True)[0][0]
            data={'solicitudTrabajoId':sid,'usuarioOperadorId':client['usuarioId'],'moneda':'ARS'}
            check(api('/api/Billetera/pagos-protegidos',dict(data,monto=1),client)[0]==400,'Client cannot replace the agreed order amount')
            status,o=api('/api/Billetera/pagos-protegidos',data,client);assert status==201,(status,o)
            assert o['fechaVencimientoLiberacion'] is None
            return o
        o=order();path=f"/api/Billetera/pagos-protegidos/{o['id']}";p=provider.add(o,101)
        for key,bad in [('transaction_amount',1),('currency_id','USD'),('collector_id',888),('external_reference','other'),('live_mode',False),('transaction_amount_refunded',1)]:
            before=p[key];p['search_ref']=o['referenciaExterna'];p[key]=bad
            check(api(path+'/mercadopago/verificar',act(client),client)[0]==409,'Reject mismatched '+key)
            p[key]=before
        check(sql('SELECT count(*) FROM "MovimientosBilletera"',fetch=True)[0][0]==0,'Invalid provider results never create ledger entries')
        # A rejected earlier attempt must not hide a later approved charge.
        rejected=dict(p,id=100,status='rejected');provider.payments={'100':rejected,**provider.payments}
        with ThreadPoolExecutor(max_workers=8) as pool:
            codes=list(pool.map(lambda _:api(path+'/mercadopago/verificar',act(client),client)[0],range(8)))
        check(codes==[200]*8,'Concurrent verifications are safely repeatable')
        check(sql('SELECT count(*) FROM "MovimientosBilletera"',fetch=True)[0][0]==2,'One credit per wallet despite concurrent verification')
        check(api(path+'/mercadopago/verificar',act(other),other)[0]==403,'Another account cannot verify this payment')
        sql('UPDATE "PagosServicioProtegidos" SET "FechaVencimientoLiberacion"=now()-interval \'1 day\' WHERE "Id"=%s',(o['id'],))
        check(api(path+'/liberar',act(other),other)[0]==403,'Expired payment cannot be released by an unrelated user')
        check(api('/api/Billetera/pagos-protegidos/liberar-vencidos',act(admin),admin)[1]==[],'Automatic release excludes unfinished work')
        with ThreadPoolExecutor(max_workers=8) as pool:
            codes=list(pool.map(lambda _:api(path+'/liberar',act(client),client)[0],range(8)))
        check(codes==[200]*8,'Concurrent release is idempotent')
        check(sql('SELECT "SaldoDisponible","SaldoRetenido" FROM "Billeteras" WHERE "UsuarioId"=%s',(pro['usuarioId'],),True)[0]==(1000,0),'Professional credited exactly once')
        # Professional enrolment is owner-only and cannot be approved manually.
        check(api('/api/PagosProfesionales',{'usuarioId':pro['usuarioId']})[0]==401,'Anonymous professional payment creation blocked')
        check(api('/api/PagosProfesionales',{'usuarioId':pro['usuarioId']},other)[0]==403,"Cannot create another account enrolment order")
        code,po=api('/api/PagosProfesionales',{'usuarioId':pro['usuarioId']},pro);assert code==201,(code,po)
        pp='/api/PagosProfesionales/'+str(po['id'])
        for endpoint,method in [('', 'GET'),('/mercadopago/preference','POST'),('/mercadopago/verificar','POST')]:
            check(api(pp+endpoint,session=other,method=method)[0]==403,'Professional payment ownership '+endpoint)
        check(api(pp+'/confirmar',session=admin,method='POST')[1]['aprobado'] is False,'Administrator cannot fabricate approval')
        provider.add(po,201)
        check(api(pp+'/confirmar',session=admin,method='POST')[1]['aprobado'] is True,'Administrator confirmation validates the actual provider payment')
        # Rollback after provider refund: retry reconciles real state instead of refunding twice.
        o2=order();path2=f"/api/Billetera/pagos-protegidos/{o2['id']}";provider.add(o2,102)
        assert api(path2+'/mercadopago/verificar',act(client),client)[0]==200
        check(api(path2+'/disputas',{'usuarioOperadorId':client['usuarioId'],'motivo':'No se realizó el trabajo contratado'},client)[0]==200,'Client can dispute a collected payment')
        resolution={'adminUserId':admin['usuarioId'],'liberarAlProfesional':False,'resolucion':'Devolver al medio de pago original'}
        provider.fail_refund=True
        check(api(path2+'/resolver-disputa',resolution,admin)[0]==502,'Provider refund failure keeps the dispute and balances unchanged')
        check(sql('SELECT "Estado" FROM "PagosServicioProtegidos" WHERE "Id"=%s',(o2['id'],),True)[0][0]=='EnDisputa','Failed refund does not mark the dispute resolved')
        provider.fail_refund=False
        sql('CREATE FUNCTION test_fail_refund() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW."Estado" = \'Reintegrado\' THEN RAISE EXCEPTION \'simulated commit failure\'; END IF; RETURN NEW; END $$')
        sql('CREATE TRIGGER test_fail_refund BEFORE UPDATE ON "PagosServicioProtegidos" FOR EACH ROW EXECUTE FUNCTION test_fail_refund()')
        check(api(path2+'/resolver-disputa',resolution,admin)[0]==500,'Injected local persistence failure rolls back the ledger')
        sql('DROP TRIGGER test_fail_refund ON "PagosServicioProtegidos"')
        check(api(path2+'/resolver-disputa',resolution,admin)[0]==200,'Retry reconciles the confirmed provider refund')
        check(sum(provider.refunds.values())==1,'Refund provider side effect occurs only once')
        check(sql('SELECT "SaldoDisponible","SaldoRetenido" FROM "Billeteras" WHERE "UsuarioId"=%s',(client['usuarioId'],),True)[0]==(0,0),'Refund does not also manufacture available wallet credit')
        check(len(api('/api/Seguridad/alertas',session=admin)[1])>0,'Administrators receive persisted payment security alerts')
        check(api('/api/Seguridad/alertas',session=other)[0]==403,'Security alerts are private to administrators')
        # Private resource ownership and no graph injection.
        address={'clienteId':client['clienteId'],'calle':'Test','numero':'123','localidad':'Test','cliente':None}
        status,addr=api('/api/Direcciones',address,client);assert status==201,(status,addr)
        check(api('/api/Direcciones/'+str(addr['id']),session=other)[0]==404,'Other accounts cannot read private addresses')
        check(api('/api/Direcciones',dict(address,clienteId=client['clienteId']),other)[0]==403,'Other accounts cannot create private addresses for an owner')
        sql('UPDATE "Usuarios" SET "Activo"=false WHERE "Id"=%s',(other['usuarioId'],))
        check(api('/api/Billetera/usuario/'+str(other['usuarioId']),session=other)[0]==401,'Suspension invalidates an already issued JWT')
        sql('UPDATE "Usuarios" SET "Activo"=true WHERE "Id"=%s',(other['usuarioId'],))
        check(api('/api/Auth/logout',{},other)[0]==204,'Logout revokes the current session')
        check(api('/api/Billetera/usuario/'+str(other['usuarioId']),session=other)[0]==401,'A logged-out JWT cannot be reused')
        statuses=[api('/api/Auth/login',{'email':'bruteforce@example.test','password':'wrong-password'},headers={'CF-Connecting-IP':str(i)})[0] for i in range(21)]
        check(statuses[-1]==429 and statuses[0]==401,'Distributed account rate limit cannot be bypassed with forged IP headers')
        # Dump and restore the isolated database, including monetary history and unique provider bindings.
        dump=temp/'isolated.dump'
        subprocess.run([str(PG/'pg_dump.exe'),'-h','127.0.0.1','-p',str(dbport),'-U','postgres','-d','postgres','-Fc','-f',str(dump)],check=True,capture_output=True)
        sql('CREATE DATABASE restore_test')
        subprocess.run([str(PG/'pg_restore.exe'),'-h','127.0.0.1','-p',str(dbport),'-U','postgres','-d','restore_test','--exit-on-error',str(dump)],check=True,capture_output=True)
        with psycopg.connect(conn.replace('dbname=postgres','dbname=restore_test')) as db:
            check(db.execute('SELECT count(*) FROM "CobrosVerificados"').fetchone()[0]==3,'Backup restores verified payment bindings and schema in an isolated database')
        # Restart with Turnstile enabled and exercise server-side validation, replay and action binding.
        stop();env.update({'Turnstile__SiteKey':'test-site','Turnstile__SecretKey':'test-secret','Turnstile__TestUrl':provider.url+'/turnstile'});start()
        data={'email':'payment1@example.test','password':'Test-password-123'}
        check(api('/api/Auth/login',data)[0]==403,'Configured Turnstile cannot be skipped')
        for token in ['invalid','valid-wrong-host','valid-register']:
            check(api('/api/Auth/login',data,headers={'X-Turnstile-Token':token})[0]==403,'Reject challenge '+token)
        h={'X-Turnstile-Token':'valid-login-1'}
        check(api('/api/Auth/login',data,headers=h)[0]==200,'Valid hostname and action allow login')
        check(api('/api/Auth/login',data,headers=h)[0]==403,'Used challenge cannot be replayed')
        # Production must reject the demo route too and refuse a loopback provider URL.
        stop();env.update({'ASPNETCORE_ENVIRONMENT':'Production','Turnstile__SiteKey':'','Turnstile__SecretKey':''});start()
        client=login('payment1@example.test','Test-password-123')
        check(api('/api/Billetera/pagos-protegidos/1/confirmar-pago-demo',act(client),client)[0]==410,'Production also retires the demo endpoint')
        check(api(path+'/mercadopago/verificar',act(client),client)[0]==503,'Production cannot redirect provider credentials to a test endpoint')
        check(True,'All isolated payment security checks passed')
    except Exception:
        print('API LOG TAIL:',(temp/'api.log').read_text(encoding='utf-8',errors='replace')[-5000:]);raise
    finally:
        stop();logfile.close();provider.server.shutdown()
        if started:subprocess.run([str(PG/'pg_ctl.exe'),'-D',str(temp/'db'),'-m','immediate','-w','stop'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=30)
        if temp.parent==Path(tempfile.gettempdir()) and temp.name.startswith('servilabs-payments-'):shutil.rmtree(temp,ignore_errors=True)
if __name__=='__main__':run()
