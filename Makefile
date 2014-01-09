
all: TestProg.exe

LIBS=LibUsbDotNet.dll

TestProg.exe: TestProg.cs DFU/dfutypes.cs DFU/dfu.cs $(LIBS)
	gmcs `pkg-config --libs LibUsbDotNet` $(filter-out $(LIBS),$^)

LibUsbDotNet.dll:
	ln -s `pkg-config --variable=Libraries LibUsbDotNet` .

clean:
	rm -f *~ \#* LibUsbDotNet.dll *.exe
