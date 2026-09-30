"""Local fake operator exercises OAuth over HTTP, never contacting a real mobile line."""
import base64
import hashlib
import http.cookiejar
import json
import threading
import urllib.request
import urllib.error
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

class Operator:
    def __init__(self):
        self.results = {'numero': {'devicePhoneNumberVerified': True}, 'titular': {'idDocumentMatch':'true','birthdateMatch':'true'}, 'sim': {'swapped':False}, 'ubicacion': {'verificationResult':'TRUE'}}
        self.codes = {}
        self.calls = []
        operator = self
        class Handler(BaseHTTPRequestHandler):
            def log_message(self,*args): pass
            def send_json(self,body,status=200):
                self.send_response(status); self.send_header('Content-Type','application/json'); self.end_headers(); self.wfile.write(json.dumps(body).encode())
            def do_GET(self):
                query = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
                code = 'code-' + str(len(operator.codes))
                operator.codes[code] = query
                self.send_response(302); self.send_header('Location',query['redirect_uri'][0] + '?' + urllib.parse.urlencode({'code':code,'state':query['state'][0]})); self.end_headers()
            def do_POST(self):
                if self.headers.get('Transfer-Encoding') == 'chunked':
                    body = b''
                    while True:
                        size = int(self.rfile.readline().strip(),16)
                        if not size: self.rfile.readline(); break
                        body += self.rfile.read(size); self.rfile.read(2)
                else: body = self.rfile.read(int(self.headers.get('Content-Length',0)))
                if self.path == '/token':
                    form = urllib.parse.parse_qs(body.decode())
                    query = operator.codes.pop(form.get('code',[''])[0],None)
                    challenge = base64.urlsafe_b64encode(hashlib.sha256(form.get('code_verifier',[''])[0].encode()).digest()).decode().rstrip('=')
                    if not query or challenge != query['code_challenge'][0] or self.headers.get('Authorization') != 'Basic ' + base64.b64encode(b'fake-client:fake-secret').decode():
                        self.send_json({'error':'invalid_grant'},400); return
                    self.send_json({'access_token':'test-access-token'}); return
                if self.headers.get('Authorization') != 'Bearer test-access-token': self.send_json({},401); return
                operator.calls.append((self.path,json.loads(body)))
                result = operator.results[self.path.strip('/')]
                if isinstance(result,int): self.send_json({'code':'OPERATOR_ERROR'},result)
                else: self.send_json(result)
        self.server = ThreadingHTTPServer(('127.0.0.1',0),Handler)
        self.thread = threading.Thread(target=self.server.serve_forever,daemon=True); self.thread.start()
        self.base = 'http://127.0.0.1:' + str(self.server.server_port)
    def env(self,base):
        return {'OPEN_GATEWAY_CLIENT_ID':'fake-client','OPEN_GATEWAY_CLIENT_SECRET':'fake-secret','OPEN_GATEWAY_TOKEN_URL':self.base+'/token','OPEN_GATEWAY_AUTHORIZE_URL':self.base+'/authorize','OPEN_GATEWAY_REDIRECT_URI':base+'/api/Verificaciones/callback','OPEN_GATEWAY_NUMBER_VERIFICATION_URL':self.base+'/numero','OPEN_GATEWAY_KYC_MATCH_URL':self.base+'/titular','OPEN_GATEWAY_SIM_SWAP_URL':self.base+'/sim','OPEN_GATEWAY_DEVICE_LOCATION_URL':self.base+'/ubicacion','OPEN_GATEWAY_MODE':'sandbox'}
    def close(self): self.server.shutdown(); self.server.server_close()

def run_checks(base,api,token,session,other_token,png,operator,db,check):
    uid = session['usuarioId']
    def upload(image,auth=token):
        boundary='public-profile-test'
        body=(f'--{boundary}\r\nContent-Disposition: form-data; name="foto"; filename="photo.png"\r\nContent-Type: image/png\r\n\r\n').encode()+image+f'\r\n--{boundary}--\r\n'.encode()
        headers={'Content-Type':'multipart/form-data; boundary='+boundary}
        if auth: headers['Authorization']='Bearer '+auth
        try: response=urllib.request.urlopen(urllib.request.Request(base+'/api/Perfiles/foto',data=body,headers=headers))
        except urllib.error.HTTPError as exc: response=exc
        return response.status
    check(upload(png,None)==401,'Public photo upload requires an authenticated owner')
    check(upload(b'not-a-real-image')==400,'Public photo rejects an invalid image signature')
    identity=api('/api/Identidad',token=token)[1]
    check(upload(png)==200,'Owner uploads a public profile photo')
    profile=api(f'/api/Perfiles/{uid}',token=other_token)[1]
    check(profile['fotoUrl'] and not any(x in profile for x in ['dni','email','telefono','fechaNacimiento']),'Public profile excludes private contact and identity fields')
    check(urllib.request.urlopen(base+profile['fotoUrl']).read()==png,'Public photo uses its separate endpoint')
    check(api('/api/Identidad',token=token)[1]==identity,'Public photo never changes identity verification')
    check(api('/api/Perfiles/foto',token=token,method='DELETE')[0]==204,'Owner can remove public photo')
    check(api(f'/api/Perfiles/{uid}',token=other_token)[1]['fotoUrl'] is None,'Removing photo restores the initials fallback')
    check(api('/api/Verificaciones')[0]==401,'Line status requires authentication')
    check(api('/api/Verificaciones/configuracion',token=token)[0]==403,'Provider configuration is restricted to administrators')
    check(api('/api/Verificaciones/iniciar',{'tipo':'numero','consentimiento':False},token=token)[0]==400,'No operator consultation without consent')
    check(api('/api/Verificaciones/iniciar',{'tipo':'ubicacion','consentimiento':True},token=token)[0]==400,'Location check requires explicit current coordinates')
    def state(kind): return next(v for v in api('/api/Verificaciones',token=token)[1]['verificaciones'] if v['tipo']==kind)
    def start(kind):
        jar=http.cookiejar.CookieJar(); opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
        body={'tipo':kind,'consentimiento':True,'latitud':-34.6,'longitud':-58.4}
        response=opener.open(urllib.request.Request(base+'/api/Verificaciones/iniciar',data=json.dumps(body).encode(),headers={'Content-Type':'application/json','Authorization':'Bearer '+token}))
        data=json.load(response)
        check('fake-secret' not in data['url'] and session['accessToken'] not in data['url'],'OAuth redirect excludes credentials and session tokens')
        return opener,data['url']
    def finish(kind):
        opener,url=start(kind); opener.open(url).read(); return state(kind)
    for kind,expected in [('numero','coincide'),('titular','coincide'),('sim','sin_cambios'),('ubicacion','coincide')]:
        result=finish(kind)
        check(result['estado']==expected and result['prueba'] and not result['verificado'],f'{kind}: real HTTP OAuth exchange and strict result mapping; sandbox grants no badge')
    check(operator.calls[0][1]['phoneNumber']=='+5491122334455','Operator receives normalized Argentine mobile number')
    check(api('/api/Identidad',token=token)[1]==identity,'Operator checks never change identity verification')
    opener,url=start('numero'); query=urllib.parse.parse_qs(urllib.parse.urlparse(url).query)
    callback='/api/Verificaciones/callback?'+urllib.parse.urlencode({'state':query['state'][0],'code':'stolen'})
    check(api(callback)[0]==400,'Callback rejects a different browser')
    opener.open(url).read()
    try: opener.open(base+callback); replay=200
    except urllib.error.HTTPError as exc: replay=exc.code
    check(replay==400,'Callback rejects replay after completion')
    check(api('/api/Verificaciones/iniciar',{'tipo':'numero','consentimiento':True},token=token)[0]==429,'Operator calls are rate limited per account')
    db.execute('DELETE FROM "VerificacionesLinea" WHERE "UsuarioId"=%s',(uid,))
    operator.results['numero']={}
    check(finish('numero')['estado']=='sin_datos','HTTP 200 without a positive result never verifies a number')
    operator.results['numero']={'devicePhoneNumberVerified':False}
    check(finish('numero')['estado']=='no_coincide','Negative number match is preserved')
    operator.results['numero']=403
    check(finish('numero')['estado']=='sin_autorizacion','Operator coverage/authorization failure remains visible')
    operator.results['numero']={'devicePhoneNumberVerified':True}
    opener,url=start('numero')
    check(api('/api/Perfiles/telefono',{'pais':'54','numero':'0261151234567'},token=token,method='PUT')[0]==400,'Argentina rejects domestic 0/15 format instead of corrupting the number')
    check(api('/api/Perfiles/telefono',{'pais':'54','numero':'2611234567'},token=token,method='PUT')[0]==200,'Phone form normalizes a ten-digit Argentine mobile number')
    try: opener.open(url); stale=200
    except urllib.error.HTTPError as exc: stale=exc.code
    check(stale==400 and state('numero')['estado']=='sin_verificar','Changing a phone invalidates old verification and pending callbacks')
    check(api('/api/Perfiles/telefono',{'pais':'598','numero':'99123456'},token=token,method='PUT')[1]['telefono']=='+59899123456','Other country phone numbers retain the selected prefix')
    api('/api/Perfiles/telefono',{'pais':'54','numero':'1122334455'},token=token,method='PUT')

    # External browser has a separate cookie jar from the native WebView.
    status,data=api('/api/Verificaciones/iniciar',{'tipo':'numero','consentimiento':True,'nativa':True},token=token)
    check(status==200 and '/verificacion-movil.html#' in data['url'],'Native flow issues a short-lived browser handoff without session tokens')
    params=urllib.parse.parse_qs(urllib.parse.urlparse(data['url']).fragment)
    body={'state':params['state'][0],'ticket':params['ticket'][0]}
    browser=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    req=urllib.request.Request(base+'/api/Verificaciones/abrir',data=json.dumps(body).encode(),headers={'Content-Type':'application/json'})
    authorization=json.load(browser.open(req))
    check(api('/api/Verificaciones/abrir',body)[0]==400,'Native browser handoff can only be redeemed once')
    completed=browser.open(authorization['url'])
    check(completed.geturl().endswith('/verificacion-finalizada.html') and state('numero')['estado']=='coincide','Native callback completes in the external browser and provides an app return link')
    check(b'servilabs://verificacion' in completed.read(),'Native return URI contains no token or personal information')
    db.execute('UPDATE "VerificacionesLinea" SET "Prueba"=false WHERE "UsuarioId"=%s AND "Tipo"=%s',(uid,'numero'))
    check(state('numero')['verificado'] is True,'Only a positive non-sandbox result may show phone verified')
    db.execute("UPDATE \"VerificacionesLinea\" SET \"Creado\"=now()-interval '31 days' WHERE \"UsuarioId\"=%s",(uid,))
    check(state('numero')['verificado'] is False,'Expired phone verification is not presented as current')
