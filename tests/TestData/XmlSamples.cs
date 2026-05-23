namespace tests.TestData;

public static class XmlSamples
{
    public static string Checklist => """
<?xml version='1.0' encoding='UTF-8'?>
<CHECKLIST>
  <ASSET>
    <ROLE>None</ROLE>
    <ASSET_TYPE>Computing</ASSET_TYPE>
    <HOST_NAME>host1</HOST_NAME>
    <HOST_IP>10.0.0.1</HOST_IP>
    <TARGET_KEY>STIG-ID-1</TARGET_KEY>
  </ASSET>
  <STIGS>
    <iSTIG>
      <STIG_INFO>
        <SI_DATA>
          <SID_NAME>title</SID_NAME>
          <SID_DATA>Microsoft Windows Security Technical Implementation Guide</SID_DATA>
        </SI_DATA>
        <SI_DATA>
          <SID_NAME>version</SID_NAME>
          <SID_DATA>2</SID_DATA>
        </SI_DATA>
        <SI_DATA>
          <SID_NAME>releaseinfo</SID_NAME>
          <SID_DATA>Release: 3 Benchmark Date: 01 Jan 2025</SID_DATA>
        </SI_DATA>
      </STIG_INFO>
      <VULN>
        <STIG_DATA>
          <VULN_ATTRIBUTE>Vuln_Num</VULN_ATTRIBUTE>
          <ATTRIBUTE_DATA>V-1001</ATTRIBUTE_DATA>
        </STIG_DATA>
        <STATUS>Not_Reviewed</STATUS>
        <FINDING_DETAILS></FINDING_DETAILS>
        <COMMENTS></COMMENTS>
        <SEVERITY_OVERRIDE></SEVERITY_OVERRIDE>
        <SEVERITY_JUSTIFICATION></SEVERITY_JUSTIFICATION>
      </VULN>
    </iSTIG>
  </STIGS>
</CHECKLIST>
""";

    public static string Xccdf => """
<?xml version='1.0' encoding='UTF-8'?>
<Benchmark id='xccdf_mil.disa.stig_benchmark_sample' xmlns:dc='http://purl.org/dc/elements/1.1/'>
  <title>Windows STIG</title>
  <description>Sample description</description>
  <status date='2026-01-15'>accepted</status>
  <version>5</version>
  <plain-text>Release: 8 Benchmark Date: 15 Jan 2026</plain-text>
  <dc:identifier>sample-target-key</dc:identifier>
</Benchmark>
""";
}
